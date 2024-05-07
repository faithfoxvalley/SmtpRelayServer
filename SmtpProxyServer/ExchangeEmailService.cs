using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Drives.Item.SharedWithMe;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Identity.Client;
using MimeKit;
using SmtpProxyServer.Config;
using System;
using System.Collections.Generic;
using System.IO;
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

        public async Task<bool> TrySendAsync(MimeMessage mimeMessage, string smtpUser)
        {
            Message msg = await TryConvertMessage(mimeMessage, smtpUser);
            if (msg == null)
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

        private async Task<Message> TryConvertMessage(MimeMessage mimeMsg, string smtpUser)
        {
            Message msg = new()
            {
                Subject = mimeMsg.Subject,
            };

            if (TryGetRecipients(mimeMsg.To, out List<Recipient> toRecipients))
                msg.ToRecipients = toRecipients;
            if (TryGetRecipients(mimeMsg.Cc, out List<Recipient> ccRecipients))
                msg.CcRecipients = ccRecipients;
            if (TryGetRecipients(mimeMsg.Bcc,out List<Recipient> bccRecipients))
                msg.BccRecipients = bccRecipients;

            if (msg.ToRecipients == null && msg.CcRecipients == null && msg.BccRecipients == null)
            {
                Log.Error("Failed to send message: No recipients");
                return null;
            }

            MailboxAddress sender = mimeMsg.Sender;
            if (sender == null || !validator.IsValid(sender.LocalPart, sender.Domain, smtpUser))
            {
                MailboxAddress from = (mimeMsg.From?.FirstOrDefault()) as MailboxAddress;
                if (from == null || !validator.IsValid(from.LocalPart, from.Domain, smtpUser))
                {
                    Log.Error("Failed to send message: No sender");
                    return null;

                }
                sender = from;
            }
            msg.Sender = CreateRecipient(sender.Address);
            msg.From = CreateRecipient(sender.Address);

            MimeAnalyzer mimeVisitor = new MimeAnalyzer();
            mimeMsg.Accept(mimeVisitor);
            
            msg.Body = new ItemBody()
            {
                ContentType = BodyType.Html,
                Content = mimeVisitor.HtmlBody,
            };

            await AddAttachments(msg, mimeVisitor.Attachments);

            return msg;
        }

        // Reference: https://stackoverflow.com/questions/30351465/html-email-with-inline-attachments-and-non-inline-attachments
        // https://learn.microsoft.com/en-us/graph/api/resources/fileattachment?view=graph-rest-1.0
        private async Task AddAttachments(Message msg, IEnumerable<MimeEntity> attachments)
        {
            List<Attachment> result = new List<Attachment>();
            foreach (MimeEntity attachment in attachments)
            {
                using (MemoryStream mem = new MemoryStream())
                {
                    FileAttachment resultAttachment = new FileAttachment();

                    ContentDisposition disposition = attachment.ContentDisposition;
                    if (disposition == null)
                        continue;
                    string fileName = disposition.FileName;
                    if (!string.IsNullOrWhiteSpace(fileName))
                        resultAttachment.Name = fileName;

                    MimeKit.ContentType contentType = attachment.ContentType;
                    if (contentType == null)
                        continue;
                    resultAttachment.ContentType = contentType.MimeType;

                    string contentId = attachment.ContentId;
                    if (!string.IsNullOrWhiteSpace(contentId))
                        resultAttachment.ContentId = contentId;

                    if (attachment.IsAttachment)
                    {
                        msg.HasAttachments = true;
                    }
                    else
                    {
                        resultAttachment.IsInline = true;
                        if (resultAttachment.ContentId == null)
                            continue;
                    }

                    await attachment.WriteToAsync(mem);
                    if (mem.Length <= 0)
                        continue;

                    resultAttachment.ContentBytes = mem.ToArray();
                    resultAttachment.Size = resultAttachment.ContentBytes.Length;

                    result.Add(resultAttachment);
                }
            }

            if(result.Count > 0)
            {
                msg.Attachments = result;
            }
        }

        private bool TryGetRecipients(InternetAddressList list, out List<Recipient> recipients)
        {
            recipients = new List<Recipient>();
            if (list == null)
                return false;
            foreach (InternetAddress to in list)
            {
                foreach(MailboxAddress email in GetAddresses(to))
                {
                    if(validator.IsValid(email.LocalPart, email.Domain))
                        recipients.Add(CreateRecipient(email.Address));
                }
            }
            return recipients.Count > 0;
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
