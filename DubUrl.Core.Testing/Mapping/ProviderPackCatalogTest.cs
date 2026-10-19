using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.Text;

namespace DubUrl.Core.Testing.Mapping;

public class ProviderPackCatalogTest
{
    [TestCase("postgresql-npgsql")]
    [TestCase("postgresql")]
    [TestCase("pg")]
    [TestCase("npgsql")]
    public void TryGet_CanonicalIdOrAlias_ResolvesCanonicalEntry(string name)
    {
        var entry = Entry("postgresql-npgsql", "postgresql", "pg", "npgsql");
        var catalog = new ProviderPackCatalog([entry]);

        var found = catalog.TryGet(name, out var resolved);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(resolved, Is.SameAs(entry));
            Assert.That(resolved!.Provider, Is.EqualTo("postgresql-npgsql"));
        });
    }

    [Test]
    public void Constructor_DuplicateAlias_RejectsAmbiguousCatalog()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ProviderPackCatalog(
        [
            Entry("postgresql-npgsql", "postgresql"),
            Entry("postgresql-odbc", "postgresql"),
        ]));

        Assert.That(exception!.Message, Does.Contain("postgresql"));
    }

    [Test]
    public void Read_VersionedOfficialCatalog_LoadsCompatibilityMetadata()
    {
        using var stream = CatalogStream("""
            {
              "schemaVersion": 1,
              "entries": [{
                "provider": "postgresql-npgsql",
                "version": "1.2.3",
                "downloadUri": "https://github.com/Seddryck/DubUrl/releases/download/v1.2.3/duburl-connector-postgresql-npgsql-1.2.3.zip",
                "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
                "aliases": ["postgresql", "postgres", "pg", "npgsql"],
                "schemes": ["pg", "pgx", "pgsql", "postgres", "postgresql"],
                "dubUrlApiRange": "[1.0.0,2.0.0)",
                "targetFrameworks": ["net8.0", "net9.0", "net10.0"],
                "runtimeIdentifiers": []
              }]
            }
            """);

        var entry = ProviderPackCatalog.Read(stream).Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entry.Provider, Is.EqualTo("postgresql-npgsql"));
            Assert.That(entry.DubUrlApiRange, Is.EqualTo("[1.0.0,2.0.0)"));
            Assert.That(entry.TargetFrameworks, Is.EquivalentTo(new[] { "net8.0", "net9.0", "net10.0" }));
            Assert.That(entry.RuntimeIdentifiers, Is.Empty);
        });
    }

    [TestCase(2, "schema version")]
    [TestCase(1, "SHA-256", "not-a-hash")]
    public void Read_InvalidOfficialCatalog_ReportsActionableFailure(
        int schemaVersion,
        string expectedMessage,
        string sha256 = "0000000000000000000000000000000000000000000000000000000000000000")
    {
        using var stream = CatalogStream($$"""
            {
              "schemaVersion": {{schemaVersion}},
              "entries": [{
                "provider": "postgresql-npgsql",
                "version": "1.2.3",
                "downloadUri": "https://example.test/postgresql.zip",
                "sha256": "{{sha256}}",
                "aliases": ["postgresql"],
                "schemes": ["postgresql"],
                "dubUrlApiRange": "[1.0.0,2.0.0)",
                "targetFrameworks": ["net8.0"],
                "runtimeIdentifiers": []
              }]
            }
            """);

        var exception = Assert.Throws<InvalidDataException>(() => ProviderPackCatalog.Read(stream));

        Assert.That(exception!.Message, Does.Contain(expectedMessage).IgnoreCase);
    }

    private static ProviderPackCatalogEntry Entry(string id, params string[] aliases)
        => new(id, "1.0.0", new Uri($"https://example.test/{id}.zip"), new string('0', 64), aliases);

    private static MemoryStream CatalogStream(string json)
        => new(Encoding.UTF8.GetBytes(json));
}
