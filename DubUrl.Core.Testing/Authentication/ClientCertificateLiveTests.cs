using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

[Explicit("Requires a certificate-required isolated database endpoint and fixture-local certificate files.")]
[NonParallelizable]
public class ClientCertificateLiveTests
{
    [TestCase("POSTGRESQL")]
    [TestCase("COCKROACHDB")]
    [TestCase("MYSQL")]
    [TestCase("MARIADB")]
    [TestCase("SINGLESTORE")]
    public async Task ValidClientSucceedsAndMissingOrUntrustedClientsFail(string database)
    {
        var prefix = "DUBURL_" + database + "_CERT_";
        var url = Required(prefix + "URL"); // URL must contain no secret and must disable pooling.
        using var certificate = X509Certificate2.CreateFromPemFile(Required(prefix + "PEM"), Required(prefix + "KEY"));
        Register();
        var registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
        var location = new ConnectionUrl(url, registry);
        var builder = new DbConnectionStringBuilder { ConnectionString = location.Parse() };
        Assert.That(builder.Keys.Cast<string>().Any(k => k.Equals("pooling", StringComparison.OrdinalIgnoreCase)
            && builder[k].ToString()!.Equals("false", StringComparison.OrdinalIgnoreCase)), Is.True, "Fresh physical connections are mandatory.");
        await using (var connection = await location.WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate)).OpenAsync())
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        }
        // This test intentionally requires server account restrictions. It must fail if a weaker
        // authentication rule accidentally permits a connection without the client certificate.
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await using var connection = await location.OpenAsync();
        });
        using var key = RSA.Create(2048);
        using var untrusted = new CertificateRequest("CN=alice", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            await using var connection = await location.WithAuthentication(ConnectionAuthentication.ClientCertificate(untrusted)).OpenAsync();
        });
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) Assert.Ignore("Missing isolated certificate fixture configuration.");
        return value!;
    }
    private static void Register()
    {
        DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnector.SingleStoreConnectorFactory.Instance);
    }
}
