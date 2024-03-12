using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace SmtpProxyServer.Config
{
    public class ConfigFile : ITomlMetadataProvider
    {
        private string fileLocation;

        public ExchangeConfig Exchange { get; set; } = new ExchangeConfig();
        public SmtpConfig Smtp { get; set; } = new SmtpConfig();
        public List<UserAccount> UserAccount { get; set; } = new List<UserAccount>();

        public ConfigFile() { }

        [IgnoreDataMember]
        public TomlPropertiesMetadata PropertiesMetadata { get; set; }

        public static bool TryLoad(string fileLocation, out ConfigFile config)
        {
            config = null;

            if (!File.Exists(fileLocation))
            {

                ConfigFile newFile = new ConfigFile();
                newFile.fileLocation = fileLocation;
                newFile.Save();
                config = newFile;
                return true;
            }

            DocumentSyntax documentSyntax = Toml.Parse(File.ReadAllText(fileLocation), fileLocation, TomlParserOptions.ParseAndValidate);
            if(documentSyntax.HasErrors)
            {
                Log.Error(DiagnosticsToString("Syntax error in config file:", documentSyntax.Diagnostics));
                return false;
            }

            if(!Toml.TryToModel(documentSyntax, out ConfigFile existingConfig, out DiagnosticsBag diagnostics))
            {
                Log.Error(DiagnosticsToString("Error reading config file:", diagnostics));
                return false;
            }

            config = existingConfig;
            config.fileLocation = fileLocation;
            config.Save();
            return true;
        }

        private static string DiagnosticsToString(string prefix, DiagnosticsBag diagnostics)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(prefix).AppendLine(); ;
            foreach (DiagnosticMessage diag in diagnostics)
                sb.Append(diag).AppendLine();
            return sb.ToString();
        }

        public void Save(string fileLocation = null)
        {
            if (fileLocation == null)
                fileLocation = this.fileLocation;

            File.WriteAllText(fileLocation, Toml.FromModel(this));
        }
    }
}
