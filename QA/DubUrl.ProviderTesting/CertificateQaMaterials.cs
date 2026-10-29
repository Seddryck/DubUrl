using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DubUrl.ProviderTesting;

/// <summary>Generated test PKI only. Client private keys never leave their certificate objects.</summary>
internal sealed class CertificateQaMaterials : IDisposable
{
    internal X509Certificate2 Authority { get; } = CreateAuthority("DubUrl-QA-CA");
    internal X509Certificate2 OtherAuthority { get; } = CreateAuthority("DubUrl-QA-other-CA");
    internal X509Certificate2 Server { get; }
    internal X509Certificate2 Client { get; }
    internal X509Certificate2 Replacement { get; }
    internal X509Certificate2 WrongIdentity { get; }
    internal X509Certificate2 Expired { get; }
    internal X509Certificate2 Untrusted { get; }
    internal CertificateQaMaterials()
    {
        Server = Issue(Authority, "localhost", server: true);
        Client = Issue(Authority, "alice");
        Replacement = Issue(Authority, "alice");
        WrongIdentity = Issue(Authority, "mallory");
        Expired = Issue(Authority, "alice", expired: true);
        Untrusted = Issue(OtherAuthority, "alice");
    }
    private static X509Certificate2 CreateAuthority(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(3));
    }
    private static X509Certificate2 Issue(X509Certificate2 authority, string name, bool server = false, bool expired = false)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + name, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2") }, true));
        if (server)
        {
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            request.CertificateExtensions.Add(names.Build());
        }
        var now = DateTimeOffset.UtcNow;
        using var issued = request.Create(authority, now.AddDays(-2), expired ? now.AddDays(-1) : now.AddDays(1), RandomNumberGenerator.GetBytes(16));
        return issued.CopyWithPrivateKey(key);
    }
    public void Dispose()
    {
        foreach (var certificate in new[] { Server, Client, Replacement, WrongIdentity, Expired, Untrusted, Authority, OtherAuthority }) certificate.Dispose();
    }
}
