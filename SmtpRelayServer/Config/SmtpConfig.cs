using System.Collections.Generic;
using System.Runtime.Serialization;
using Tomlyn.Model;

namespace SmtpRelayServer.Config;

public class SmtpConfig : ITomlMetadataProvider
{
    public string HostName { get; set; } = "localhost";
    public List<string> EmailDomainFilter { get; set; } = new List<string>();
    public List<string> ConnectionSubnetFilter { get; set; } = new List<string>();
    public string Certificate { get; set; } = "";
    public string CertificatePassword { get; set; } = "";

    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }

    public SmtpConfig()
    {

    }
}
