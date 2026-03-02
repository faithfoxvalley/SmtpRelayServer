using System.Collections.Generic;
using System.Linq;
using SmtpRelayServer.Config;

namespace SmtpRelayServer;

public class AccountValidator
{
    private readonly HashSet<string> domains;
    private readonly Dictionary<string, string> mailboxes;
    private readonly Dictionary<string, string> accounts;

    public AccountValidator(IEnumerable<string> domains, IEnumerable<UserAccount> accounts)
    {
        foreach (UserAccount account in accounts)
            account.ValidateConfig();
        this.domains = new HashSet<string>(domains.Select(x => x.ToLowerInvariant()));
        mailboxes = accounts.ToDictionary(x => x.Username, x => x.ExchangeEmail.ToLowerInvariant());
        this.accounts = accounts.ToDictionary(x => x.Username, x => x.Password);
    }

    /*public bool IsValid(string email, bool isSender)
    {
        if(email == null)
        {
            Log.Warn($"Tried to send email {(isSender ? "from" : "to")} an invalid mailbox: (null)");
            return false;
        }
        if (!string.IsNullOrWhiteSpace(domain) && !email.EndsWith("@" + domain, StringComparison.InvariantCultureIgnoreCase))
        {
            Log.Warn($"Tried to send email {(isSender ? "from" : "to")} an invalid mailbox: {email}");
            return false;
        }
        return !isSender || mailboxes.Contains(email.ToLowerInvariant());
    }*/

    public bool IsValid(string user, string domain, string senderUser = null)
    {
        bool isSender = senderUser != null;

        if (user == null || domain == null)
        {
            Log.Warn($"Tried to send email {(isSender ? "from" : "to")} an invalid mailbox: {user ?? "(null)"}@{domain ?? "null"}");
            return false;
        }

        domain = domain.ToLowerInvariant();
        if (domains.Count > 0 && !domains.Contains(domain))
        {
            Log.Warn($"Tried to send email {(isSender ? "from" : "to")} an invalid mailbox: {user}@{domain}");
            return false;
        }


        if (isSender)
        {
            string email = user.ToLowerInvariant() + "@" + domain;
            if (mailboxes.TryGetValue(senderUser, out string actualEmail))
            {
                if(actualEmail != email)
                {
                    Log.Warn($"Tried to send email from a user with different mailbox:  got:{email} expected:{actualEmail}");
                    return false;
                }
            }
            else
            {
                Log.Warn($"Tried to send email from a user without a mailbox: {email}");
                return false;
            }
        }

        return true;
    }

    public bool IsValidLogin(string username, string password)
    {
        if (accounts.TryGetValue(username, out string userPassword))
        {
            bool correctPassword = userPassword == password;
            if (!correctPassword)
                Log.Warn("Invalid password for " + username);
            return correctPassword;
        }
        Log.Warn("User account not found: " + username);
        return false;
    }
}
