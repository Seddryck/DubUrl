using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.Loader;
using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
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

        var registry = new ProviderCatalogBuilder().AddModule(ConnectorFixture.Create()).Build();
        Assert.That(new ConnectionUrl("pg://localhost/demo", registry).Parse(), Does.Contain("demo"));
    }

    [Test]
    public async Task InvalidActiveModule_ReportsLoadDiagnostic()
    {
        await Install(databaseName: "Unknown database");
        var error = Assert.Throws<ProviderPackException>(() => new ConnectionUrl("pg://localhost/demo"));
        Assert.That(error!.Code, Is.EqualTo("module-load-failed"));
        Assert.That(error.Message, Does.Contain("postgresql-npgsql"));
    }

    [Test]
    public async Task InactiveVersions_AreNotLoaded()
    {
        await Install(version: "0.0.1", databaseName: "Unknown database");
        await Install(version: "0.0.2");
        Assert.That(new ConnectionUrl("pg://localhost/demo").Parse(), Does.Contain("demo"));
    }

    [Test]
    public async Task InvalidDefinition_IsNotLoaded_HealthyConnectorStillWorks()
    {
        var corrupt = await Install("corrupt");
        await File.WriteAllTextAsync(Path.Combine(corrupt.InstallPath, ConnectorDefinition.FileName), "invalid JSON");
        await Install();

        Assert.That(new ConnectionUrl("pg://localhost/demo").Parse(), Does.Contain("demo"));
    }

    private static async Task<InstalledProviderPack> Install(
        string provider = "postgresql-npgsql", string version = "0.0.1", string? databaseName = null)
    {
        var manifest = ConnectorFixture.Definition with
        {
            Provider = provider, Version = version, DatabaseName = databaseName ?? "PostgreSQL"
        };
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var stream = zip.CreateEntry(ConnectorDefinition.FileName).Open())
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            foreach (var name in new[] { manifest.Assembly, "Npgsql.dll" })
            {
                var assembly = Assembly.Load(Path.GetFileNameWithoutExtension(name));
                using var asset = zip.CreateEntry(name).Open();
                asset.Write(await File.ReadAllBytesAsync(assembly.Location));
            }
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
        var registry = new ProviderCatalogBuilder().AddModule(module).Build();
        var context = AssemblyLoadContext.GetLoadContext(registry.GetProviderFactory(["pg"]).GetType().Assembly)!;

        var resolved = context.LoadFromAssemblyName(host.GetName());

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.SameAs(host));
            Assert.That(context.Assemblies.Select(assembly => assembly.GetName().Name), Does.Not.Contain(name));
        });
    }
}
