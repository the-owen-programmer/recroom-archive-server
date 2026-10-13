using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace RecServer.Infrastructure;

/// <summary>
/// Issues a server certificate on demand for whatever host name the client asks for (TLS SNI), signed by the local test CA
/// (server/certs/ca.pem + ca.key). Together with the wildcard DNS in the network namespace this means no host the client
/// tries can fail on certificate trouble, and every name it asks for shows up in the request log.
/// </summary>
public class CertificateAuthority
{
    private readonly X509Certificate2 _ca;
    private readonly RSA _caKey;
    private readonly ConcurrentDictionary<string, X509Certificate2> _issued = new(StringComparer.OrdinalIgnoreCase);

    public CertificateAuthority(string certDir)
    {
        var caPem = File.ReadAllText(Path.Combine(certDir, "ca.pem"));
        _ca = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(caPem[PemEncoding.Find(caPem).Base64Data]));
        _caKey = RSA.Create();
        _caKey.ImportFromPem(File.ReadAllText(Path.Combine(certDir, "ca.key")));
    }

    public X509Certificate2 For(string? host)
    {
        host = string.IsNullOrWhiteSpace(host) ? "localhost" : host.ToLowerInvariant();
        return _issued.GetOrAdd(host, Issue);
    }

    private X509Certificate2 Issue(string host)
    {
        using var key = RSA.Create(2048);
        var req = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        if (System.Net.IPAddress.TryParse(host, out var ip)) san.AddIpAddress(ip); else san.AddDnsName(host);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
        var serial = new byte[12]; RandomNumberGenerator.Fill(serial); serial[0] &= 0x7F;
        var gen = X509SignatureGenerator.CreateForRSA(_caKey, RSASignaturePadding.Pkcs1);
        using var cert = req.Create(_ca.SubjectName, gen, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(800), serial);
        using var withKey = cert.CopyWithPrivateKey(key);
        // Kestrel/SslStream on Linux needs a key that can be exported/re-imported; round-trip through PKCS#12.
        var pfx = withKey.Export(X509ContentType.Pfx, "x");
        Console.WriteLine($"issued certificate for {host}");
        return X509CertificateLoader.LoadPkcs12(pfx, "x");
    }
}
