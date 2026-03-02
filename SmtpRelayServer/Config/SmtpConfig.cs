using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using Tomlyn.Model;

namespace SmtpRelayServer.Config;

public class SmtpConfig : ITomlMetadataProvider
{
    public string HostName { get; set; } = "localhost";
    public List<string> EmailDomainFilter { get; set; } = new List<string>();
    public List<string> ConnectionSubnetFilter { get; set; } = new List<string>();
    public string Certificate { get; set; } = "";
    public string CertificateKey { get; set; } = "";
    public string CertificatePassword { get; set; } = "";

    [IgnoreDataMember]
    public TomlPropertiesMetadata PropertiesMetadata { get; set; }

    public SmtpConfig()
    {

    }

    public bool TryReadCertificate(out X509Certificate2 cert)
    {
        cert = null;
        if (!File.Exists(Certificate))
            return false;

        string password = null;
        if (!string.IsNullOrWhiteSpace(CertificatePassword))
            password = CertificatePassword;
        X509ContentType certType = X509Certificate2.GetCertContentType(Certificate);

        switch (certType)
        {
            case X509ContentType.Cert:
                string keyFile = null;
                if (!string.IsNullOrWhiteSpace(CertificateKey))
                    keyFile = CertificateKey;

                if (password != null)
                    cert = X509Certificate2.CreateFromEncryptedPemFile(Certificate, CertificatePassword, keyFile);
                else
                    cert = X509Certificate2.CreateFromPemFile(Certificate, keyFile);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) // Windows doesn't support pem files
                    cert = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pkcs12), null);
                break;
            case X509ContentType.Pfx:
                cert = X509CertificateLoader.LoadPkcs12FromFile(Certificate, password);
                break;
            default:
                throw new IOException($"SSL certificate format '{certType}' not supported.");
        }

        if (!cert.Verify())
            Log.Warn("Provided SSL certificate could not be verified");
        return true;
    }
}
