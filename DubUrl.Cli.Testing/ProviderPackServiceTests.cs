using DubUrl.Cli;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ProviderPackServiceTests
{
    [Test]
    public void List_CuratedConnector_ReportsCanonicalIdAliasesAndSchemesAsAvailable()
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
                Assert.That(connector.Activation, Is.EqualTo("inactive"));
                Assert.That(connector.Status, Is.EqualTo("available"));
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
        var entry = new ProviderPackCatalogEntry(
            "postgresql-npgsql",
            "1.0.0",
            new Uri("https://example.test/postgresql-npgsql.zip"),
            new string('0', 64),
            ["postgresql", "pg", "npgsql"],
            ["postgresql", "postgres", "pg"]);
        return new(new ProviderPackStore(root), new ProviderPackCatalog([entry]), new HttpClient());
    }

    private static string CreateStorePath()
        => Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-store-{Guid.NewGuid():N}");
}
