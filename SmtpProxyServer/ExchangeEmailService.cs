using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Identity.Client;
using MimeKit;
using SmtpProxyServer.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmtpProxyServer
{
    public class ExchangeEmailService
    {
        private readonly GraphServiceClient graphClient;
        private readonly AccountValidator validator;

        public ExchangeEmailService(ExchangeConfig config, AccountValidator validator)
        {
            string tenantId = config.TenantId;
            string clientId = config.ClientId;
            string clientSecret = config.ClientSecret;

            ClientSecretCredential credential = new(tenantId, clientId, clientSecret);
            graphClient = new GraphServiceClient(credential);
            this.validator = validator;
        }

        public async Task<bool> SendAsync(MimeMessage mimeMessage, string smtpUser)
        {
            if (!TryConvertMessage(mimeMessage, smtpUser, out Message msg))
                return false;

            SendMailPostRequestBody sendMail = new SendMailPostRequestBody()
            {
                SaveToSentItems = false,
                Message = msg,
            };

            string from = msg.Sender.EmailAddress.Address;
            try
            {
                await graphClient.Users[from].SendMail.PostAsync(sendMail);
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending message from {from}: {e}");
                return false;
            }

            Log.Info($"Sent message from {from}");
            return true;
        }

        private bool TryConvertMessage(MimeMessage mimeMsg, string smtpUser, out Message msg)
        {
            msg = new()
            {
                Subject = mimeMsg.Subject,
            };

            msg.ToRecipients = new List<Recipient>();
            msg.CcRecipients = new List<Recipient>();
            msg.BccRecipients = new List<Recipient>();
            if (!TryCopyRecipients(mimeMsg.To, msg.ToRecipients) 
                & !TryCopyRecipients(mimeMsg.Cc, msg.CcRecipients)
                & !TryCopyRecipients(mimeMsg.Bcc, msg.BccRecipients))
            {
                Log.Error("Failed to send message: No recipients");
                return false;
            }

            MailboxAddress sender = mimeMsg.Sender;
            if (sender == null || !validator.IsValid(sender.LocalPart, sender.Domain, smtpUser))
            {
                MailboxAddress from = (mimeMsg.From?.FirstOrDefault()) as MailboxAddress;
                if (from == null || !validator.IsValid(from.LocalPart, from.Domain, smtpUser))
                {
                    Log.Error("Failed to send message: No sender");
                    return false;

                }
                sender = from;
            }
            Recipient senderObj = CreateRecipient(sender.Address);
            msg.Sender = senderObj;
            msg.From = senderObj;

            string plainText = mimeMsg.TextBody;
            if(plainText == null)
            {
                string html = mimeMsg.HtmlBody;
                if(html == null)
                {
                    string contentType = mimeMsg.Body?.ContentType?.MimeType;
                    if(contentType == null)
                        Log.Error("Failed to send message: Invalid email content type");
                    else
                        Log.Error("Failed to send message: Invalid email content type " + contentType);
                    return false;
                }
                else
                {
                    msg.Body = new ItemBody()
                    {
                        ContentType = BodyType.Html,
                        Content = html,
                    };
                }
            }
            else
            {
                msg.Body = new ItemBody()
                {
                    ContentType = BodyType.Text,
                    Content = plainText,
                };
            }
            return true;
        }

        private List<Attachment> GetAttachments(IEnumerable<MimeEntity> attachments)
        {
            if (attachments == null)
                return null;

            List<Attachment> result = new List<Attachment>();
            foreach(MimeEntity att in attachments)
            {
                if (!att.IsAttachment)
                    continue;

            }

            if (result.Count == 0)
                return null;
            return result;
        }

        private bool TryCopyRecipients(InternetAddressList list, List<Recipient> destination)
        {
            if (list == null)
                return false;
            foreach (InternetAddress to in list)
            {
                foreach(MailboxAddress email in GetAddresses(to))
                {
                    if(validator.IsValid(email.LocalPart, email.Domain))
                        destination.Add(CreateRecipient(email.Address));
                }
            }
            return destination.Count > 0;
        }

        private IEnumerable<MailboxAddress> GetAddresses(InternetAddress address)
        {
            if(address is GroupAddress group)
            {
                foreach(InternetAddress subAddress in group.Members ?? new InternetAddressList())
                {
                    if (subAddress is MailboxAddress subMailbox)
                        yield return subMailbox;
                }
            }
            else if(address is MailboxAddress mailbox)
            {
                yield return mailbox;
            }
        }

        private Recipient CreateRecipient(string email)
        {
            return new Recipient()
            {
                EmailAddress = new EmailAddress()
                {
                    Address = email
                }
            };
        }
    }
}
