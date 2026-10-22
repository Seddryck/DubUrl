using System.Reflection;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class ConsolidatedArchiveTest
{
    [TestCase("postgresql-npgsql", "cockroachdb-npgsql", 5)]
    [TestCase("postgresql-npgsql", "cratedb-npgsql", 5)]
    [TestCase("postgresql-npgsql", "questdb-npgsql", 5)]
    [TestCase("postgresql-npgsql", "timescale-npgsql", 5)]
    [TestCase("postgresql-odbc", "questdb-odbc", 3)]
    [TestCase("postgresql-odbc", "timescale-odbc", 3)]
    [TestCase("mysql-mysqlconnector", "mariadb-mysqlconnector", 2)]
    [TestCase("mysql-odbc", "mariadb-odbc", 2)]
    public void DatabaseInstallationName_ResolvesToSharedArchiveAndEveryMapping(string canonical, string alias, int databases)
    {
        var catalog = BundledProviderCatalog.Create();
        Assert.That(catalog.TryGet(canonical, out var definition), Is.True);
        Assert.That(catalog.TryGet(alias, out var resolved), Is.True);
        Assert.That(resolved, Is.SameAs(definition));
        Assert.That(definition!.Databases, Has.Count.EqualTo(databases));
        var registry = new ProviderCatalogBuilder().AddModule(AttributeProviderModule.FromConnector(definition, Load)).Build();
        var factory = registry.GetProviderFactory([definition.Schemes.First()]);
        foreach (var database in definition.Databases)
            foreach (var scheme in database.Schemes)
                Assert.That(registry.GetProviderFactory(DubUrl.Parsing.SchemeKey.Parse(scheme)), Is.SameAs(factory));
    }

    [TestCase("missing")]
    [TestCase("extra")]
    [TestCase("wrong-scheme")]
    [TestCase("duplicate")]
    [TestCase("wrong-driver")]
    public void Loader_RejectsIncompleteOrIncorrectMappingInventory(string failure)
    {
        BundledProviderCatalog.Create().TryGet("postgresql-npgsql", out var definition);
        var databases = definition!.Databases.ToArray();
        var invalid = failure switch
        {
            "missing" => definition with { Databases = databases.Skip(1).ToArray() },
            "extra" => definition with { Databases = [.. databases, new("Unknown", ["unknown"])] },
            "duplicate" => definition with { Databases = [.. databases.Skip(1), databases[1]] },
            "wrong-driver" => definition with { Driver = "Missing.Driver" },
            _ => definition with { Databases = [databases[0] with { Schemes = ["incorrect"] }, .. databases.Skip(1)] }
        };
        Assert.Throws<InvalidOperationException>(() => AttributeProviderModule.FromConnector(invalid, Load));
    }

    private static Assembly Load(string path) => Assembly.Load(Path.GetFileNameWithoutExtension(path));

    [TestCase("postgresql-npgsql")]
    [TestCase("postgresql-odbc")]
    public void Loader_UsesSchemesAndDriver_NotDatabaseDisplayNames(string provider)
    {
        BundledProviderCatalog.Create().TryGet(provider, out var definition);
        var renamed = definition! with
        {
            Databases = definition.Databases.Select(database => database with { Name = "Renamed for presentation" }).ToArray()
        };
        var registry = new ProviderCatalogBuilder().AddModule(AttributeProviderModule.FromConnector(renamed, Load)).Build();
        foreach (var scheme in definition.Schemes)
            Assert.That(registry.CanHandle(scheme), Is.True);
    }
}
