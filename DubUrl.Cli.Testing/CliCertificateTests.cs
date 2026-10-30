using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Application;
using DubUrl.Authentication;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

[NonParallelizable]
public class CliCertificateTests
{
    private string directory = null!;
    private string FilePath(string name) => Path.Combine(directory, name);
    [SetUp]
    public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "duburl-cli-cert-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var key = RSA.Create(2048);
        using var cert = new CertificateRequest("CN=alice", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        File.WriteAllText(FilePath("client.crt"), cert.ExportCertificatePem());
        File.WriteAllText(FilePath("client.key"), key.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(FilePath("encrypted.key"), key.ExportEncryptedPkcs8PrivateKeyPem("sentinel", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        File.WriteAllBytes(FilePath("client.pfx"), cert.Export(X509ContentType.Pkcs12, "sentinel"));
        File.WriteAllBytes(FilePath("client.p12"), cert.Export(X509ContentType.Pkcs12, ""));
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
    }
    [TearDown]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable("DUBURL_CERT_TEST_URL", null);
        foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
    [TestCase("pem", "direct"), TestCase("pfx", "named"), TestCase("p12", "environment"), TestCase("encrypted", "direct")]
    public async Task CommandsUseSharedLibraryAndDistinctPasswordSources(string form, string selection)
    {
        var console = new ConsoleStub(true);
        var secrets = new Secrets();
        var check = new Check();
        const string url = "mysql://alice@localhost/app";
        Environment.SetEnvironmentVariable("DUBURL_CERT_TEST_URL", url);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:test"] = url }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets, credentialStore: secrets);
        string[] target = selection == "named" ? ["--connection", "test"] : selection == "environment" ? ["--connection-env", "DUBURL_CERT_TEST_URL"] : [url];
        string[] certificate = form is "pem" or "encrypted"
            ? ["--client-certificate", FilePath("client.crt"), "--client-key", FilePath(form == "pem" ? "client.key" : "encrypted.key")]
            : ["--client-certificate", FilePath("client." + form)];
        string[] password = form == "pfx" ? ["--certificate-password-prompt"] : form == "encrypted" ? ["--certificate-credential-store", "key"] : [];
        var exit = await CliHost.InvokeAsync(["connection", "check", .. target, .. certificate, .. password,
            "--ca-certificate", FilePath("client.crt"), "--credential-store", "database", "--engine"], new CheckServices(services, check));
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(check.Calls, Is.EqualTo(1));
        Assert.That(secrets.Prompts, Is.EqualTo(form == "pfx" ? 1 : 0));
        Assert.That(secrets.Reads, Is.EqualTo(form == "encrypted" ? 2 : 1));
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
        Assert.That(() => check.Certificate!.GetCertHash(), Throws.Exception, "CLI must dispose its loaded certificate after the connection is disposed.");
    }
    [TestCase("--client-key", "key")]
    [TestCase("--certificate-password-prompt")]
    [TestCase("--certificate-credential-store", "key")]
    [TestCase("--client-certificate", "client.crt")]
    [TestCase("--client-certificate", "client.p12", "--client-key", "key")]
    [TestCase("--client-certificate", "client.pfx", "--certificate-password-prompt", "--certificate-credential-store", "key")]
    [TestCase("--client-certificate", "client.pfx", "--os-identity")]
    [TestCase("--client-certificate", "client.pfx", "--oauth", "browser")]
    public async Task ConflictsNeverAcquireSecrets(params string[] arguments)
    {
        var console = new ConsoleStub(true); var secrets = new Secrets();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets, credentialStore: secrets);
        var exit = await CliHost.InvokeAsync(["connection", "check", "mysql://alice@localhost/app", .. arguments], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(secrets.Reads + secrets.Prompts, Is.Zero);
    }
    [TestCase("denied", 5), TestCase("cancelled", 130), TestCase("wrong", 5), TestCase("empty", 0)]
    public async Task StoreFailuresAndEmptyPasswordsAreHandledSafely(string behavior, int expected)
    {
        var console = new ConsoleStub(false); var secrets = new Secrets { Behavior = behavior };
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], credentialStore: secrets);
        var extension = behavior == "empty" ? "p12" : "pfx";
        var exit = await CliHost.InvokeAsync(["connection", "check", "mysql://alice@localhost/app", "--client-certificate", FilePath("client." + extension),
            "--certificate-credential-store", "key"], new CheckServices(services, new Check()));
        Assert.That(exit, Is.EqualTo(expected), console.Errors.ToString());
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
    }
    [Test]
    public async Task NoninteractivePromptFailsWithoutReadingTerminal()
    {
        var console = new ConsoleStub(false); var secrets = new Secrets();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets);
        var exit = await CliHost.InvokeAsync(["connection", "check", "mysql://alice@localhost/app", "--client-certificate", FilePath("client.pfx"), "--certificate-password-prompt"], new CheckServices(services, new Check()));
        Assert.That(exit, Is.EqualTo(5)); Assert.That(secrets.Prompts, Is.Zero);
    }
    [Test]
    public async Task SchemaDryRunUsesMetadataWithoutFilesOrSecrets()
    {
        var console = new ConsoleStub(false); var secrets = new Secrets();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:test"] = "mysql://alice@localhost/app" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets, credentialStore: secrets);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run", "--client-certificate", FilePath("absent.pfx"), "--certificate-password-prompt"], services);
        Assert.That(exit, Is.Zero, console.Errors.ToString()); Assert.That(secrets.Prompts + secrets.Reads, Is.Zero);
    }
    [Test]
    public async Task UnsupportedCertificateRouteNeverAcquiresSecrets()
    {
        var console = new ConsoleStub(true); var secrets = new Secrets();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets, credentialStore: secrets);
        var exit = await CliHost.InvokeAsync(["connection", "check", "mssql://alice@localhost/app", "--client-certificate", FilePath("client.pfx"), "--certificate-password-prompt"], services);
        Assert.That(exit, Is.EqualTo(2)); Assert.That(secrets.Prompts + secrets.Reads, Is.Zero);
    }
    [Test]
    public async Task SchemaDeploymentUsesSharedCertificateStrategy()
    {
        var console = new ConsoleStub(true); var secrets = new Secrets(); var check = new Check();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:test"] = "mysql://alice@localhost/app" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: secrets, credentialStore: secrets);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--client-certificate", FilePath("client.pfx"), "--certificate-password-prompt"], new CheckServices(services, check));
        Assert.That(exit, Is.Zero, console.Errors.ToString()); Assert.That(check.Calls, Is.EqualTo(1)); Assert.That(secrets.Prompts, Is.EqualTo(1));
        Assert.That(() => check.Certificate!.GetCertHash(), Throws.Exception);
    }
    private sealed class DeploymentCheck(Check check) : ISchemaDeploymentService
    {
        public async Task<SchemaDeploymentResult> DeployAsync(DubUrl.Schema.Documents.ISchemaDocument document, ConnectionUrl target, SchemaDeploymentOptions? options = null, CancellationToken cancellationToken = default)
        {
            await check.CheckAsync(target, new(), cancellationToken);
            return new(true, 1, "MySql", null, []);
        }
    }
    [Test]
    public async Task CapabilitiesAdvertiseCertificateOptionsWithoutAcquiringSecrets()
    {
        var console = new ConsoleStub(false); var secrets = new Secrets();
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: secrets, credentialStore: secrets);
        var exit = await CliHost.InvokeAsync(["capabilities", "--json"], services);
        Assert.That(exit, Is.Zero);
        foreach (var option in new[] { "--client-certificate", "--client-key", "--ca-certificate", "--certificate-password-prompt", "--certificate-credential-store" })
            Assert.That(console.Output.ToString(), Does.Contain(option));
        Assert.That(secrets.Prompts + secrets.Reads, Is.Zero);
    }
    private sealed class CheckServices(IServiceProvider inner, Check check) : IServiceProvider
    { public object? GetService(Type type) => type == typeof(IConnectionCheckService) ? check : type == typeof(ISchemaDeploymentService) ? new DeploymentCheck(check) : inner.GetService(type); }
    private sealed class Check : IConnectionCheckService
    {
        public int Calls { get; private set; }
        public X509Certificate2? Certificate { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            await using var connection = (MySqlConnector.MySqlConnection)await target.ConnectAsync(cancellationToken);
            var settings = new MySqlConnector.MySqlConnectionStringBuilder(connection.ConnectionString);
            Assert.That(settings.SslMode, Is.EqualTo(MySqlConnector.MySqlSslMode.VerifyFull));
            var certificates = new X509CertificateCollection();
            await connection.ProvideClientCertificatesCallback!(certificates);
            Certificate = (X509Certificate2)certificates[0];
            Assert.That(Certificate.HasPrivateKey, Is.True);
            Assert.That(target.Url, Is.EqualTo("mysql://alice@localhost/app"));
            return new(true, "engine", "MySqlConnector", 0, []);
        }
    }
    private sealed class Secrets : IOsCredentialStore, IHiddenPasswordPrompt
    {
        public string? Behavior { get; init; }
        public int Prompts { get; private set; }
        public int Reads { get; private set; }
        public ValueTask<string> ReadAsync(CancellationToken cancellationToken = default) { Prompts++; return new("sentinel"); }
        public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        {
            Reads++;
            if (Behavior == "denied") throw new InvalidOperationException("sentinel denied");
            if (Behavior == "cancelled") throw new OperationCanceledException(cancellationToken);
            return new(new UsernamePasswordCredentials(reference == "database" ? "alice" : "ignored", Behavior == "empty" ? "" : Behavior == "wrong" ? "wrong" : "sentinel"));
        }
    }
    private sealed class ConsoleStub(bool interactive) : ICliConsole
    {
        public StringWriter Output { get; } = new(); public StringWriter Errors { get; } = new();
        public TextReader In => new StringReader("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"customer\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\"}]}]}");
        public TextWriter Out => Output; public TextWriter Error => Errors;
        public bool IsInteractive => interactive; public bool IsInputRedirected => !interactive;
    }
}
