using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Tomlyn;
using Tomlyn.Extensions.Configuration;
using Tomlyn.Model;

namespace SmtpRelayServer.Config;

public class ConfigFile : ITomlMetadataProvider
{
    private string fileLocation;

    public ExchangeConfig Exchange { get; set; } = new ExchangeConfig();
    public SmtpConfig Smtp { get; set; } = new SmtpConfig();
    public List<UserAccount> UserAccount { get; set; } = new List<UserAccount>();

    public ConfigFile() { }

    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }

    public static ConfigFile Load(string fileLocation, string envPrefix)
    {
        IConfigurationBuilder builder = new ConfigurationBuilder();

        ConfigFile result = new ConfigFile();
        if (TryGetTomlConfig(fileLocation, out IConfiguration tomlconfig))
        {
            builder = builder.AddConfiguration(tomlconfig);
            result.fileLocation = fileLocation;
        }

        builder = builder.AddEnvironmentVariables(envPrefix);

        IConfiguration config = builder.Build();
        config.Bind(result);
        return result;
    }

    private static bool TryGetTomlConfig(string fileLocation, out IConfiguration config)
    {
        config = null;
        if(string.IsNullOrEmpty(fileLocation)  || !File.Exists(fileLocation))
            return false;

        IConfigurationBuilder builder = new ConfigurationBuilder()
            .AddTomlFile(fileLocation, true, false);

        config = builder.Build().RemoveUnderscores();

        ConfigFile tomlOnlyMap = new ConfigFile();
        config.Bind(tomlOnlyMap);
        tomlOnlyMap.fileLocation = fileLocation;
        tomlOnlyMap.Save();
        return true;
    }

    public void Save(string fileLocation = null)
    {
        if (fileLocation == null)
            fileLocation = this.fileLocation;

        string folder = Path.GetDirectoryName(fileLocation);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);
        File.WriteAllText(fileLocation, Toml.FromModel(this));
    }


}
