using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Mapping;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;
using SingleStoreConnector;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class ClientCertificateAuthenticationTests
{
    [TestCase("postgres://alice@localhost/app")]
    [TestCase("cockroach://alice@localhost/app")]
    [TestCase("mysql://alice@localhost/app")]
    [TestCase("mariadb://alice@localhost/app")]
    [TestCase("singlestore://alice@localhost/app")]
    public async Task FiveNativeAdaptersInstallCertificateObjectsAndRequireServerVerification(string url)
    {
        using var key = RSA.Create(2048);
        using var certificate = Certificate(key);
        var target = new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate));
        _ = target.Dialect;
        using var connection = await target.ConnectAsync();
        var certificates = new X509CertificateCollection();
        switch (connection)
        {
            case NpgsqlConnection pg:
#pragma warning disable CS0618 // Version-tested per-connection hook; no per-connection data source is created.
                pg.ProvideClientCertificatesCallback!(certificates);
#pragma warning restore CS0618
                Assert.That(new NpgsqlConnectionStringBuilder(pg.ConnectionString).SslMode, Is.EqualTo(SslMode.VerifyFull));
                break;
            case MySqlConnection mysql:
                await mysql.ProvideClientCertificatesCallback!(certificates);
                Assert.That(new MySqlConnectionStringBuilder(mysql.ConnectionString).SslMode, Is.EqualTo(MySqlSslMode.VerifyFull));
                break;
            case SingleStoreConnection single:
                await single.ProvideClientCertificatesCallback!(certificates);
                Assert.That(new SingleStoreConnectionStringBuilder(single.ConnectionString).SslMode, Is.EqualTo(SingleStoreSslMode.VerifyFull));
                break;
            default: Assert.Fail("Unexpected provider."); break;
        }
        Assert.That(certificates.Count, Is.EqualTo(1));
        Assert.That(certificates[0], Is.SameAs(certificate));
        Assert.That(certificate.HasPrivateKey, Is.True);
        Assert.That(target.Url, Is.EqualTo(url));
    }

    [TestCase("postgres://alice@localhost/app?SSL%20Mode=Disable")]
    [TestCase("mysql://alice@localhost/app?SslMode=Preferred")]
    [TestCase("postgres://alice:@localhost/app")]
    [TestCase("postgres://alice@localhost/app?SSL%20Certificate=sentinel")]
    public void ConflictingSourcesAndWeakTlsAreRejected(string url)
    {
        using var key = RSA.Create(2048);
        using var certificate = Certificate(key);
        Assert.That(() => new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate)).Connect(),
            Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("CredentialConflict"));
    }

    [TestCase("mssql://localhost/app")]
    [TestCase("sqlite:///:memory:")]
    [TestCase("snowflake://alice@account/app")]
    public void RejectsProvidersWithoutClientCertificateLogin(string url)
    {
        using var key = RSA.Create(2048);
        using var certificate = Certificate(key);
        var registry = Registry();
        // Provider may be unavailable; either way external certificate configuration must never succeed.
        Assert.That(() => new ConnectionUrl(url, registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate)).Connect(),
            Throws.TypeOf<ConnectionAuthenticationException>());
    }

    [TestCase("mysql://alice@localhost/app")]
    [TestCase("mariadb://alice@localhost/app")]
    public async Task SupplementalCredentialsAreExplicitAndResolvedOnce(string url)
    {
        using var key = RSA.Create(2048);
        using var certificate = Certificate(key);
        var source = new Source();
        var target = new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate,
            new() { SupplementalCredentials = source }));
        using var connection = await target.ConnectAsync();
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(new MySqlConnectionStringBuilder(connection.ConnectionString).Password, Is.EqualTo("sentinel"));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
    }

    private static ISchemeRegistry Registry()
    {
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnectorFactory.Instance);
        return new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }

    [TestCase("Microsoft.Data.SqlClient", "mssql")]
    [TestCase("Npgsql", "questdb")]
    [TestCase("Npgsql", "timescale")]
    [TestCase("MySql.Data.MySqlClient", "mysql")]
    public async Task UnsupportedCertificateContextRejectsBeforeSourceAcquisition(string provider, string scheme)
    {
        using var key = RSA.Create(2048);
        using var certificate = Certificate(key);
        var source = new Source();
        var strategy = ConnectionAuthentication.ClientCertificate(certificate, new() { SupplementalCredentials = source });
        var context = new AuthenticationContext(provider, scheme, "localhost", null, "app", "alice");
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await strategy.ResolveAsync(context));
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        Assert.That(source.Calls, Is.Zero);
        Assert.That(error.InnerException, Is.Null);
        Assert.That(certificate.HasPrivateKey, Is.True);
    }
    private static X509Certificate2 Certificate(RSA key)
        => new CertificateRequest("CN=alice", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    private sealed class Source : IUsernamePasswordCredentialSource
    {
        public int Calls { get; private set; }
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; return new(new UsernamePasswordCredentials("alice", "sentinel")); }
    }
}
