using System;
using System.Runtime.Serialization;
using Tomlyn.Model;

namespace SmtpRelayServer.Config;

public class UserAccount : ITomlMetadataProvider
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ExchangeEmail { get; set; } = string.Empty;

    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }

    public UserAccount() { }

    internal void ValidateConfig()
    {
        if(string.IsNullOrWhiteSpace(Username))
            throw new ArgumentNullException("username");
        if(string.IsNullOrWhiteSpace(Password))
            throw new ArgumentNullException("password");
        if(string.IsNullOrWhiteSpace(ExchangeEmail))
            throw new ArgumentNullException("exchange_email");
    }
}
