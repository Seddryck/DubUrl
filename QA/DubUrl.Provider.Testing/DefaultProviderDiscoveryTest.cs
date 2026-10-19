using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.Loader;
using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Npgsql;
using DubUrl.Querying.Dialects;
using DubUrl.Rewriting;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Provider.Testing;

[NonParallelizable]
public class DefaultProviderDiscoveryTest
{
    private string? previousStore;
    private string root = null!;

    [SetUp]
    public void SetUp()
    {
        previousStore = Environment.GetEnvironmentVariable("DUBURL_PROVIDER_STORE");
        root = Path.Combine(Path.GetTempPath(), "duburl-discovery-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("DUBURL_PROVIDER_STORE", root);
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("DUBURL_PROVIDER_STORE", previousStore);
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Test]
    public async Task DefaultConstructor_DiscoversPostgreSqlWithoutCompatibilityPackage()
    {
        await Install();
        var url = new ConnectionUrl("pg://localhost/demo");
        var settings = new NpgsqlConnectionStringBuilder(url.Parse());

        Assert.Multiple(() =>
        {
            Assert.That(settings.Host, Is.EqualTo("localhost"));
            Assert.That(settings.Database, Is.EqualTo("demo"));
            Assert.That(url.Dialect.GetType().Name, Is.EqualTo("PgsqlDialect"));
            Assert.That(AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetName().Name),
                Does.Not.Contain("DubUrl"));
            Assert.That(SchemeRegistryBuilder.GetDefaultDialectRegistry("pg").Get("pg").GetType().Name,
                Is.EqualTo("PgsqlDialect"));
        });
    }

    [Test]
    public async Task NewDefaults_ObserveInstallationAndRemoval_ExistingUrlsRetainTheirRegistry()
    {
        Assert.Throws<SchemeNotFoundException>(() => new ConnectionUrl("pg://localhost/demo").Parse());
        Assert.That(Directory.Exists(root), Is.False, "Discovery must not create a missing store.");
        await Install();
        var installed = new ConnectionUrl("pg://localhost/demo");
        Assert.That(installed.Parse(), Does.Contain("demo"));

        new ProviderPackStore().Remove("postgresql-npgsql");

        Assert.Throws<SchemeNotFoundException>(() => new ConnectionUrl("pg://localhost/demo").Parse());
        Assert.That(installed.Parse(), Does.Contain("demo"));
    }

    [Test]
    public async Task ConflictingInstalledConnectors_ReportAlias_ExplicitRegistryRemainsIsolated()
    {
        await Install("first");
        await Install("second");
        var error = Assert.Throws<ProviderRegistrationException>(() => new ConnectionUrl("pg://localhost/demo"));
        Assert.That(error!.Alias, Is.EqualTo("pg"));

        var registry = new ProviderCatalogBuilder().AddModule(new ProviderModule()).Build();
        Assert.That(new ConnectionUrl("pg://localhost/demo", registry).Parse(), Does.Contain("demo"));
    }

    [Test]
    public async Task InvalidActiveModule_ReportsLoadDiagnostic()
    {
        await Install(moduleType: "Missing.ProviderModule");
        var error = Assert.Throws<ProviderPackException>(() => new ConnectionUrl("pg://localhost/demo"));
        Assert.That(error!.Code, Is.EqualTo("module-load-failed"));
        Assert.That(error.Message, Does.Contain("postgresql-npgsql"));
    }

    [Test]
    public async Task InactiveVersions_AreNotLoaded()
    {
        await Install(version: "0.0.1", moduleType: "Missing.ProviderModule");
        await Install(version: "0.0.2");
        Assert.That(new ConnectionUrl("pg://localhost/demo").Parse(), Does.Contain("demo"));
    }

    [Test]
    public async Task CorruptPack_IsNotLoaded_HealthyConnectorStillWorks()
    {
        var corrupt = await Install("corrupt");
        await File.AppendAllTextAsync(Path.Combine(corrupt.InstallPath, "module.dll"), "tampered");
        await Install();

        Assert.That(new ConnectionUrl("pg://localhost/demo").Parse(), Does.Contain("demo"));
    }

    private static async Task<InstalledProviderPack> Install(
        string provider = "postgresql-npgsql", string version = "0.0.1", string? moduleType = null)
    {
        var bytes = await File.ReadAllBytesAsync(typeof(ProviderModule).Assembly.Location);
        var manifest = new ConnectorDefinition
        {
            Provider = provider,
            Version = version,
            DubUrlApiRange = "[0.0.0,1.0.0)",
            Module = new() { Assembly = "module.dll", Type = moduleType ?? typeof(ProviderModule).FullName! },
            Schemes = ["pg"],
            TargetFrameworks = [$"net{Environment.Version.Major}.0"],
            RuntimeIdentifiers = [],
            Assets = [new() { Path = "module.dll", Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), Kind = ProviderPackAssetKind.Managed }],
        };
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var stream = zip.CreateEntry(ConnectorDefinition.FileName).Open())
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var asset = zip.CreateEntry("module.dll").Open();
            asset.Write(bytes);
        }
        archive.Position = 0;
        var digest = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return await new ProviderPackStore().InstallAsync(
            manifest with { DownloadUri = new Uri("https://packages.example/connector.zip"), Sha256 = digest }, archive);
    }

    [TestCase("Didot.Core")]
    [TestCase("Newtonsoft.Json")]
    public async Task InstalledConnector_SharesCoreDependencies_EvenWhenLocalCopiesExist(string name)
    {
        var pack = await Install();
        var host = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name));
        File.Copy(host.Location, Path.Combine(pack.InstallPath, name + ".dll"));
        using var loader = new InstalledProviderModuleLoader();
        var module = loader.LoadActive(new ProviderPackStore()).Single();
        var context = AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!;

        var resolved = context.LoadFromAssemblyName(host.GetName());

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.SameAs(host));
            Assert.That(context.Assemblies.Select(assembly => assembly.GetName().Name), Does.Not.Contain(name));
        });
    }
}
