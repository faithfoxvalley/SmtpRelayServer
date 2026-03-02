using System.Runtime.Serialization;
using Tomlyn.Model;

namespace SmtpRelayServer.Config;

public class ExchangeConfig : ITomlMetadataProvider
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;


    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }

    public ExchangeConfig() { }
}
