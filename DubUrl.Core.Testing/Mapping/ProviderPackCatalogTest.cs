using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.Text;
using System.Text.Json;

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
                "module": { "assembly": "PostgreSql.dll", "type": "PostgreSql.Module" },
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
                "module": { "assembly": "PostgreSql.dll", "type": "PostgreSql.Module" },
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

    [Test]
    public void LocalDefinition_AndEnrichedCatalog_UseTheSameModel()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var local = Entry("postgresql-npgsql", "pg") with { DownloadUri = null, Sha256 = null };
        var localJson = JsonSerializer.Serialize(local, options);
        var restored = JsonSerializer.Deserialize<ConnectorDefinition>(localJson, options)!;
        var distributed = restored with { DownloadUri = new Uri("https://example.test/pg.zip"), Sha256 = new string('0', 64) };
        using var catalogStream = new MemoryStream();
        JsonSerializer.Serialize(catalogStream, new ProviderPackCatalogDocument(1, [distributed]), options);
        catalogStream.Position = 0;
        var entry = ProviderPackCatalog.Read(catalogStream).Entries.Single();

        Assert.Multiple(() =>
        {
            Assert.That(localJson, Does.Not.Contain("downloadUri").And.Not.Contain("\"sha256\":null"));
            Assert.That(entry.Module, Is.EqualTo(local.Module));
            Assert.That(localJson, Does.Not.Contain("\"assets\""));
            Assert.That(entry.Aliases, Is.EqualTo(local.Aliases));
            Assert.That(JsonSerializer.Serialize(entry with { DownloadUri = null, Sha256 = null }, options), Is.EqualTo(localJson));
        });
    }

    [TestCase("module")]
    [TestCase("schemes")]
    public void Read_IncompleteDefinition_IsRejected(string field)
    {
        var entry = Entry("postgresql-npgsql");
        entry = field == "module" ? entry with { Module = new() { Assembly = "", Type = "" } } : entry with { Schemes = [] };
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, new ProviderPackCatalogDocument(1, [entry]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => ProviderPackCatalog.Read(stream));
    }

    private static ConnectorDefinition Entry(string id, params string[] aliases)
        => new()
        {
            Provider = id, Version = "1.0.0", DownloadUri = new Uri($"https://example.test/{id}.zip"),
            Sha256 = new string('0', 64), Aliases = aliases, Schemes = ["pg"],
            DubUrlApiRange = "[1.0.0,2.0.0)", TargetFrameworks = ["net8.0"], RuntimeIdentifiers = [],
            Module = new() { Assembly = "PostgreSql.dll", Type = "PostgreSql.Module" },

        };

    private static MemoryStream CatalogStream(string json)
        => new(Encoding.UTF8.GetBytes(json));
}
