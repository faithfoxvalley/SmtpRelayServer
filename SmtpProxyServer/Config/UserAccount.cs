using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Tomlyn.Model;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace SmtpProxyServer.Config
{
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
}
