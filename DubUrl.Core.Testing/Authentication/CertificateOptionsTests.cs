using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class CertificateOptionsTests
{
    private string directory = null!;
    private RSA key = null!;
    private X509Certificate2 certificate = null!;
    private ISchemeRegistry registry = null!;
    private string FilePath(string name) => Path.Combine(directory, name);
    [TestCase(true), TestCase(false)]
    public async Task IndependentEncryptionPreservesClientCallbackAndLegacyCaInEitherOrder(bool authenticationFirst)
    {
        using var options = new X509CertificateOptions(certificate);
        var strategy = ConnectionAuthentication.ClientCertificate(options, FilePath("client.crt"));
        var original = new ConnectionUrl("postgres://alice@localhost/app", registry);
        var target = authenticationFirst ? original.WithAuthentication(strategy).WithEncryption(ConnectionEncryption.Tls(FilePath("client.crt")))
            : original.WithEncryption(ConnectionEncryption.Tls(FilePath("client.crt"))).WithAuthentication(strategy);
        await using var connection = (NpgsqlConnection)await target.ConnectAsync();
        Assert.That(new NpgsqlConnectionStringBuilder(connection.ConnectionString).RootCertificate, Is.EqualTo(FilePath("client.crt")));
        Assert.That(Invoke(connection)[0], Is.SameAs(certificate));
        Assert.That(connection.ConnectionString, Does.Contain(options.PoolIdentity));
        Assert.That(target.Url, Is.EqualTo(original.Url));
        var conflict = target.WithEncryption(ConnectionEncryption.Tls(FilePath("other.crt")));
        Assert.Throws<ArgumentException>(() => conflict.ValidateAuthentication());
    }
    [SetUp]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "duburl-cert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        key = RSA.Create(2048);
        certificate = new CertificateRequest("CN=alice", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        File.WriteAllText(FilePath("client.crt"), certificate.ExportCertificatePem());
        File.WriteAllText(FilePath("client.key"), key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(FilePath("encrypted.key"), key.ExportEncryptedPkcs8PrivateKeyPem("sentinel", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySql.Data", MySql.Data.MySqlClient.MySqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
        registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }
    [TearDown]
    public void Cleanup()
    {
        certificate.Dispose(); key.Dispose();
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
    [TestCase("pem", false), TestCase("pem", true), TestCase("pfx", false), TestCase("pfx", true), TestCase("p12", false), TestCase("p12", true)]
    public async Task FileFormsLoadLazilyAndConfigureCa(string form, bool asynchronous)
    {
        using CertificateOptions options = form == "pem" ? new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"))
            : Bundle(form, "sentinel");
        var url = "postgres://alice@localhost/app";
        var target = new ConnectionUrl(url, registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options, FilePath("client.crt")));
        target.ValidateAuthentication();
        _ = target.Dialect;
        using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        var pg = (NpgsqlConnection)connection;
        var settings = new NpgsqlConnectionStringBuilder(pg.ConnectionString);
        Assert.That(settings.RootCertificate, Is.EqualTo(FilePath("client.crt")));
        Assert.That(settings.SslMode, Is.EqualTo(SslMode.VerifyFull));
        var certificates = Invoke(pg);
        Assert.That(((X509Certificate2)certificates[0]).HasPrivateKey, Is.True);
        Assert.That(certificates[0].GetCertHashString(), Is.EqualTo(certificate.GetCertHashString()));
        Assert.That(target.Url, Is.EqualTo(url));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
    }
    private Pkcs12CertificateOptions Bundle(string extension, string? password)
    {
        var path = FilePath("client." + extension);
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, password));
        return new(path, password);
    }
    [Test]
    public async Task EncryptedPemPasswordIsSeparateAndAcquiredOnceForConcurrentConnections()
    {
        var source = new Source("sentinel");
        using var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("encrypted.key")) { DecryptionPasswordSource = source };
        var target = new ConnectionUrl("postgres://alice@localhost/app", registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
        target.ValidateAuthentication(); _ = target.Dialect;
        Assert.That(source.Calls, Is.Zero);
        var connections = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ => await target.ConnectAsync()));
        foreach (var connection in connections) connection.Dispose();
        Assert.That(source.Calls, Is.EqualTo(1));
    }
    [TestCase("missing"), TestCase("malformed"), TestCase("mismatched"), TestCase("bad-password"), TestCase("bad-ca"), TestCase("no-key")]
    public void InvalidMaterialIsSafe(string failure)
    {
        using var otherKey = RSA.Create(2048);
#pragma warning disable SYSLIB0057
        using var publicOnly = new X509Certificate2(certificate.RawData);
#pragma warning restore SYSLIB0057
        if (failure == "malformed") File.WriteAllText(FilePath("client.crt"), "secret-invalid-material");
        if (failure == "mismatched") File.WriteAllText(FilePath("client.key"), otherKey.ExportPkcs8PrivateKeyPem());
        using CertificateOptions options = failure == "bad-password" ? new X509CertificateOptions(FilePath("client.crt"), FilePath("encrypted.key"), "wrong-secret")
            : failure == "no-key" ? new X509CertificateOptions(publicOnly)
            : new X509CertificateOptions(FilePath(failure == "missing" ? "absent.crt" : "client.crt"), FilePath("client.key"));
        if (failure == "bad-ca") File.WriteAllText(FilePath("ca.crt"), "secret-invalid-material");
        var target = new ConnectionUrl("postgres://alice@localhost/app", registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options, failure == "bad-ca" ? FilePath("ca.crt") : null));
        var error = Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.InvalidCertificate));
        Assert.That(error.ToString(), Does.Not.Contain("secret").And.Not.Contain(directory));
        Assert.That(error.InnerException, Is.Null);
    }
    [Test]
    public async Task FileCertificateIsOwnedButExistingCertificateIsBorrowed()
    {
        var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        var target = new ConnectionUrl("postgres://alice@localhost/app", registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
        using var first = (NpgsqlConnection)await target.ConnectAsync();
        var loaded = (X509Certificate2)Invoke(first)[0];
        using var second = (NpgsqlConnection)await target.ConnectAsync();
        Assert.That(Invoke(second)[0], Is.SameAs(loaded));
        first.Dispose(); second.Dispose(); options.Dispose(); options.Dispose();
        Assert.That(() => loaded.GetCertHash(), Throws.Exception);
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>());
        using (var borrowed = new X509CertificateOptions(certificate))
        {
            using var connection = await new ConnectionUrl("postgres://alice@localhost/app", registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(borrowed)).ConnectAsync();
        }
        Assert.That(certificate.HasPrivateKey, Is.True);
    }
    [Test]
    public async Task CancellationAndSynchronousSourceRequirementsArePreserved()
    {
        var source = new Source("sentinel");
        using var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("encrypted.key")) { DecryptionPasswordSource = source };
        var target = new ConnectionUrl("postgres://alice@localhost/app", registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await target.ConnectAsync(cancellation.Token));
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired));
        Assert.That(source.Calls, Is.Zero);
        using var connection = await target.ConnectAsync();
        Assert.That(source.Calls, Is.EqualTo(1));
    }
    [TestCase("?SslMode=Disable"), TestCase("?TrustServerCertificate=true"), TestCase("?SSL%20CA=other.crt"), TestCase("?Pqopt=sslmode=disable"), TestCase("?Password=sentinel")]
    public void ConflictsFailBeforeDecryption(string query)
    {
        var source = new Source("sentinel");
        using var options = new Pkcs12CertificateOptions(FilePath("absent.p12")) { DecryptionPasswordSource = source };
        var target = new ConnectionUrl("postgres://alice@localhost/app" + query, registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(source.Calls, Is.Zero);
    }
    [TestCase("mysql")]
    public async Task ConnectorNetMapsPasswordlessBundleAndPem(string scheme)
    {
        foreach (var bundle in new[] { false, true })
        {
            using CertificateOptions options = bundle ? Bundle("p12", "") : new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
            var location = new ConnectionUrl($"{scheme}://alice@localhost/app", MySqlOperatingSystemIdentityTests.Registry("MySql.Data"));
            _ = location.Parse();
            var target = location.WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
            using var connection = await target.ConnectAsync();
            var builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connection.ConnectionString);
            Assert.That(builder.SslMode, Is.EqualTo(MySql.Data.MySqlClient.MySqlSslMode.VerifyFull));
            if (bundle) Assert.That(builder.CertificateFile, Is.EqualTo(FilePath("client.p12")));
            else Assert.That(builder.SslCert, Is.EqualTo(FilePath("client.crt")));
        }
    }
    [TestCase("postgres", "PostgreSQL Unicode", true), TestCase("postgres", "PostgreSQL ANSI", false)]
    [TestCase("mysql", "MySQL ODBC 8.0 Unicode Driver", true), TestCase("mysql", "MySQL ODBC 9.5 Driver", false)]
    [TestCase("mariadb", "MariaDB ODBC 3.2 Driver", true)]
    public void OdbcMapsValidatedPemWithoutLoadingNativeDriver(string scheme, string driver, bool customCa)
    {
        using var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        var strategy = ConnectionAuthentication.ClientCertificate(options, customCa ? FilePath("client.crt") : null);
        var context = new AuthenticationContext("System.Data.Odbc", scheme + "+odbc", "localhost", null, "app", "alice");
        strategy.Resolve(context);
        using var connection = new System.Data.Odbc.OdbcConnection();
        var builder = new DbConnectionStringBuilder(true) { ["Driver"] = driver, ["Server"] = "localhost", ["Uid"] = "alice" };
        connection.ConnectionString = builder.ConnectionString;
        strategy.Configure(connection, context);
        builder.ConnectionString = connection.ConnectionString;
        if (scheme == "postgres")
        {
            Assert.That(builder["SSLmode"], Is.EqualTo("verify-full"));
            Assert.That(builder["Pqopt"].ToString(), Does.Contain("sslcert=").And.Contain("sslkey=").And.Contain("sslrootcert=").And.Contain("require_auth=none").And.Contain("sslcertmode=require"));
            if (!customCa) Assert.That(builder["Pqopt"].ToString(), Does.Contain("'system'"));
        }
        else
        {
            Assert.That(builder["SSLCERT"], Is.EqualTo(FilePath("client.crt")));
            Assert.That(builder["SSLKEY"], Is.EqualTo(FilePath("client.key")));
            if (scheme == "mysql") Assert.That(builder["SSLMODE"], Is.EqualTo("VERIFY_IDENTITY"));
            else { Assert.That(builder["SSLVERIFY"].ToString(), Is.EqualTo("1")); Assert.That(builder["FORCETLS"].ToString(), Is.EqualTo("1")); }
            Assert.That(builder.ContainsKey("SSLCA"), Is.EqualTo(customCa));
        }
    }
    [Test]
    public void OdbcRejectsUnknownDriverAndUnsupportedFormsWithoutDecryption()
    {
        var source = new Source("sentinel");
        using var bundle = new Pkcs12CertificateOptions(FilePath("missing.pfx")) { DecryptionPasswordSource = source };
        var context = new AuthenticationContext("System.Data.Odbc", "postgres+odbc", "localhost", null, "app", "alice");
        Assert.That(() => ConnectionAuthentication.ClientCertificate(bundle).Resolve(context),
            Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        Assert.That(source.Calls, Is.Zero);
        using var pem = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        var strategy = ConnectionAuthentication.ClientCertificate(pem);
        using var connection = new System.Data.Odbc.OdbcConnection("Driver={Unknown Driver};Server=localhost");
        Assert.That(() => strategy.Configure(connection, context), Throws.TypeOf<ConnectionAuthenticationException>());
    }
    [Test]
    public async Task PoolsRemainReusableWithinOptionsAndIsolatedAcrossOptions()
    {
        using var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        using var otherOptions = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        var location = new ConnectionUrl("postgres://alice@localhost/app?Application%20Name=myapp", registry);
        var target = location.WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
        using var first = await target.ConnectAsync();
        using var second = await target.ConnectAsync();
        using var other = await location.WithAuthentication(ConnectionAuthentication.ClientCertificate(otherOptions)).ConnectAsync();
        Assert.That(first.ConnectionString, Is.EqualTo(second.ConnectionString));
        Assert.That(first.ConnectionString, Is.Not.EqualTo(other.ConnectionString));
        Assert.That(new NpgsqlConnectionStringBuilder(first.ConnectionString).ApplicationName, Does.StartWith("myapp;DubUrlCertificate_"));
        Assert.That(new NpgsqlConnectionStringBuilder(first.ConnectionString).Pooling, Is.True);
    }
    [TestCase("postgres"), TestCase("cockroach"), TestCase("timescale"), TestCase("mysql"), TestCase("mariadb"), TestCase("singlestore")]
    public async Task CallbackRoutesShareFileOptionsTrustAndPoolLifetime(string scheme)
    {
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnector.SingleStoreConnectorFactory.Instance);
        using var options = new X509CertificateOptions(FilePath("client.crt"), FilePath("client.key"));
        using var connection = await new ConnectionUrl($"{scheme}://alice@localhost/app", registry)
            .WithAuthentication(ConnectionAuthentication.ClientCertificate(options, FilePath("client.crt"))).ConnectAsync();
        var callback = (Delegate)connection.GetType().GetProperty("ProvideClientCertificatesCallback")!.GetValue(connection)!;
        var certificates = new X509CertificateCollection();
        var result = callback.DynamicInvoke(certificates);
        if (result is ValueTask task) await task;
        else if (result is Task asynchronous) await asynchronous;
        Assert.That(((X509Certificate2)certificates[0]).HasPrivateKey, Is.True);
        var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        var caKey = builder.Keys.Cast<string>().Single(k => k.Replace(" ", "").Equals(connection is NpgsqlConnection ? "RootCertificate" : "SslCa", StringComparison.OrdinalIgnoreCase));
        Assert.That(builder[caKey], Is.EqualTo(FilePath("client.crt")));
        Assert.That(connection.GetType().GetMethod("ClearPool", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [connection.GetType()], null), Is.Not.Null);
    }
    private static X509CertificateCollection Invoke(NpgsqlConnection connection)
    {
        var certificates = new X509CertificateCollection();
#pragma warning disable CS0618
        connection.ProvideClientCertificatesCallback!(certificates);
#pragma warning restore CS0618
        return certificates;
    }
    private sealed class Source(string password) : IUsernamePasswordCredentialSource
    {
        public int Calls { get; private set; }
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; return new(new UsernamePasswordCredentials("ignored-store-user", password)); }
    }
}
