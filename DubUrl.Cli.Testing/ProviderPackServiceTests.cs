using DubUrl.Cli;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ProviderPackServiceTests
{
    [Test]
    public void List_CuratedConnector_ReportsCanonicalIdAliasesAndSchemesAsListed()
    {
        var root = CreateStorePath();
        try
        {
            var service = CreateService(root);

            var connector = service.List().Single();

            Assert.Multiple(() =>
            {
                Assert.That(connector.Id, Is.EqualTo("postgresql-npgsql"));
                Assert.That(connector.Source, Is.EqualTo("catalog"));
                Assert.That(connector.State, Is.EqualTo("listed"));
                Assert.That(connector.Listed, Is.True);
                Assert.That(connector.Installed, Is.False);
                Assert.That(connector.Active, Is.False);
                Assert.That(connector.Unhealthy, Is.False);
                Assert.That(connector.Aliases, Is.EquivalentTo(new[] { "postgresql", "pg", "npgsql" }));
                Assert.That(connector.Schemes, Is.EquivalentTo(new[] { "postgresql", "postgres", "pg" }));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase(ProviderPackStatus.Installed, "installed", false, false)]
    [TestCase(ProviderPackStatus.Active, "active", true, false)]
    [TestCase(ProviderPackStatus.Incompatible, "unhealthy", false, true)]
    [TestCase(ProviderPackStatus.Incomplete, "unhealthy", false, true)]
    [TestCase(ProviderPackStatus.Corrupt, "unhealthy", false, true)]
    public void ToCliPack_ReportsIndependentInstallationAndHealthFacts(
        ProviderPackStatus status,
        string state,
        bool active,
        bool unhealthy)
    {
        var entry = CreateEntry();
        var pack = new InstalledProviderPack(CreateManifest(), "unused", status, []);

        var connector = ProviderPackService.ToCliPack(pack, entry);

        Assert.Multiple(() =>
        {
            Assert.That(connector.State, Is.EqualTo(state));
            Assert.That(connector.Listed, Is.True);
            Assert.That(connector.Installed, Is.True);
            Assert.That(connector.Active, Is.EqualTo(active));
            Assert.That(connector.Unhealthy, Is.EqualTo(unhealthy));
        });
    }

    [Test]
    public void Uninstall_AliasForMissingConnector_ReportsCanonicalId()
    {
        var root = CreateStorePath();
        try
        {
            var service = CreateService(root);

            var exception = Assert.Throws<ProviderPackCommandException>(() => service.Uninstall("postgresql"));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("connector-not-installed"));
                Assert.That(exception.Message, Does.Contain("postgresql-npgsql"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static ProviderPackService CreateService(string root)
    {
        var entry = CreateEntry();
        return new(new ProviderPackStore(root), new ProviderPackCatalog([entry]), new HttpClient());
    }

    private static ProviderPackCatalogEntry CreateEntry()
        => new(
            "postgresql-npgsql",
            "1.0.0",
            new Uri("https://example.test/postgresql-npgsql.zip"),
            new string('0', 64),
            ["postgresql", "pg", "npgsql"],
            ["postgresql", "postgres", "pg"]);

    private static ProviderPackManifest CreateManifest()
        => new()
        {
            Provider = "postgresql-npgsql",
            Version = "1.0.0",
            DubUrlApiRange = "*",
            Module = new ProviderPackModuleEntry { Assembly = "connector.dll", Type = "Connector.Module" },
            Schemes = ["postgresql", "postgres", "pg"],
            TargetFrameworks = ["net10.0"],
            RuntimeIdentifiers = [],
            Assets = [],
        };

    private static string CreateStorePath()
        => Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-store-{Guid.NewGuid():N}");
}
