using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Tomlyn.Model;

namespace SmtpProxyServer.Config
{
    public class SmtpConfig : ITomlMetadataProvider
    {
        public string HostName { get; set; } = "localhost";
        public List<string> EmailDomainFilter { get; set; } = new List<string>();
        public string Certificate { get; set; } = "";
        public string CertificatePassword { get; set; } = "";

        [IgnoreDataMember]
        public TomlPropertiesMetadata PropertiesMetadata { get; set; }

        public SmtpConfig()
        {

        }
    }
}
