using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Testing.Mapping;

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

    private static ProviderPackCatalogEntry Entry(string id, params string[] aliases)
        => new(id, "1.0.0", new Uri($"https://example.test/{id}.zip"), new string('0', 64), aliases);
}
