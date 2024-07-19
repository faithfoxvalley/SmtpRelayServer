using SmtpProxyServer.Config;
using SmtpServer;
using SmtpServer.Authentication;
using SmtpServer.ComponentModel;
using SmtpServer.Mail;
using SmtpServer.Net;
using SmtpServer.Protocol;
using SmtpServer.Storage;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmtpProxyServer
{
    public class SmtpService
    {
        private readonly MessageStore messageStore;
        private readonly DomainMailboxFilter mailboxFilter;
        private readonly UserAuthenticator authenticator;
        private readonly SmtpConfig config;
        private SmtpServer.SmtpServer smtpServer;

        public SmtpService(SmtpConfig config, AccountValidator validator, ExchangeEmailService emailService)
        {
            messageStore = new MessageStore(emailService);
            mailboxFilter = new DomainMailboxFilter(validator);
            authenticator = new UserAuthenticator(validator);
            this.config = config;
        }

        public async Task Start(CancellationToken cancelToken)
        {
            ServiceProvider serviceProvider = new ServiceProvider();
            serviceProvider.Add(authenticator);
            serviceProvider.Add(messageStore);
            serviceProvider.Add(mailboxFilter);

            SmtpServerOptionsBuilder options = new SmtpServerOptionsBuilder()
                .ServerName(config.HostName);

            if (string.IsNullOrWhiteSpace(config.Certificate))
            {
                options = options.Endpoint(x => x.Port(25).AuthenticationRequired(true).AllowUnsecureAuthentication(true));
            }
            else
            {
                byte[] data = await File.ReadAllBytesAsync(config.Certificate);

                X509Certificate2 cert;
                if (string.IsNullOrWhiteSpace(config.CertificatePassword))
                    cert = new X509Certificate2(data);
                else
                    cert = new X509Certificate2(data, config.CertificatePassword);

                options = options.Endpoint(x => x.Port(465).IsSecure(true).Certificate(cert));
                options = options.Endpoint(x => x.Port(587).AuthenticationRequired(true).AllowUnsecureAuthentication(false).Certificate(cert));
            }

            smtpServer = new SmtpServer.SmtpServer(options.Build(), serviceProvider);
            await smtpServer.StartAsync(cancelToken);
        }

        public Task WaitForExit()
        {
            return smtpServer.ShutdownTask;
        }


        public class MessageStore : IMessageStore
        {
            private readonly ExchangeEmailService emailService;

            public MessageStore(ExchangeEmailService emailService)
            {
                this.emailService = emailService;
            }

            public async Task<SmtpResponse> SaveAsync(ISessionContext context, IMessageTransaction transaction, ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
            {
                if (context?.Authentication == null || !context.Authentication.IsAuthenticated)
                {
                    Log.Warn("Cannot send email without authentication");
                    return SmtpResponse.AuthenticationFailed;
                }

                try
                {
                    await using MemoryStream stream = new MemoryStream();

                    SequencePosition position = buffer.GetPosition(0);
                    while (buffer.TryGet(ref position, out ReadOnlyMemory<byte> memory))
                    {
                        await stream.WriteAsync(memory, cancellationToken);
                    }
                    stream.Position = 0;

                    MimeKit.MimeMessage message = await MimeKit.MimeMessage.LoadAsync(stream, cancellationToken);
                    if (await emailService.TrySendAsync(message, context.Authentication.User))
                        return SmtpResponse.Ok;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while reading an incoming smtp message:");
                }
                return SmtpResponse.TransactionFailed;
            }
        }

        public class DomainMailboxFilter : IMailboxFilter
        {
            private readonly AccountValidator validator;

            public DomainMailboxFilter(AccountValidator validator)
            {
                this.validator = validator;
            }

            public Task<bool> CanAcceptFromAsync(ISessionContext context, IMailbox from, int size, CancellationToken cancellationToken)
            {
                return Task.FromResult(IsValid(from, context?.Authentication));
            }

            public Task<bool> CanDeliverToAsync(ISessionContext context, IMailbox to, IMailbox from, CancellationToken token)
            {
                return Task.FromResult(IsValid(from, context?.Authentication) && IsValid(to));
            }

            private bool IsValid(IMailbox user)
            {
                return validator.IsValid(user.User, user.Host);
            }

            private bool IsValid(IMailbox user, AuthenticationContext auth)
            {
                if(auth == null || !auth.IsAuthenticated)
                {
                    Log.Warn("Cannot check mailbox address without authentication");
                    return false;
                }
                bool result = validator.IsValid(user.User, user.Host, auth.User);
                return result;
            }
        }

        private class UserAuthenticator : IUserAuthenticator
        {
            private readonly AccountValidator validator;

            public UserAuthenticator(AccountValidator validator)
            {
                this.validator = validator;
            }

            public Task<bool> AuthenticateAsync(ISessionContext context, string user, string password, CancellationToken cancellationToken)
            {
                return Task.FromResult(validator.IsValidLogin(user, password));
            }
        }
    }
}
