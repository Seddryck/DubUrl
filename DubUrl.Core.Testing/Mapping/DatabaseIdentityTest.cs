using DubUrl.Locating;
using DubUrl.Mapping;
using DubUrl.Provider.Odbc.Connectivity;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class DatabaseIdentityTest
{
    [Test]
    public void Discovery_RetainsDatabaseTypeNameAndAliases_ForNativeAndWrapperMappings()
    {
        var mappings = Discover();
        Assert.That(mappings, Has.Length.EqualTo(4));
        foreach (var mapping in mappings)
        {
            Assert.That(mapping.Database, Is.Not.Null);
            Assert.That(mapping.Database!.Name, Is.EqualTo("Analytics for Everyone"));
            Assert.That(mapping.Database.Type, Is.AnyOf(typeof(FirstDatabase), typeof(SecondDatabase)));
            Assert.That(mapping.Database.Aliases, Is.EqualTo(mapping.Database.Type == typeof(FirstDatabase)
                ? new[] { "first-db" } : new[] { "second-db" }));
        }
    }

    [Test]
    public void SameDisplayNames_DoNotMergeDistinctDatabasesSharingMapperDriverAndDialect()
    {
        var mappings = Discover().Where(mapping => mapping.ProviderInvariantName == "System.Data.Odbc").ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(mappings, Has.Length.EqualTo(2));
            Assert.That(mappings.Select(mapping => mapping.Database!.Type).Distinct().Count(), Is.EqualTo(2));
            Assert.That(mappings.Select(mapping => mapping.DatabaseName), Is.All.EqualTo("ODBC for Analytics for Everyone"));
            Assert.That(mappings.SelectMany(mapping => mapping.Aliases), Is.EquivalentTo(new[] { "odbc+first-db", "odbc+second-db" }));
            Assert.That(mappings.All(mapping => mapping.Aliases.Length == 1), Is.True);
        });
    }

    [TestCase("Identity.Driver")]
    [TestCase("System.Data.Odbc")]
    public void Selection_UsesExistingDatabaseAliases_RegardlessOfDisplayName(string driver)
    {
        var mappings = Discover();
        var selected = AttributeProviderModule.SelectMapping(mappings, "FIRST-DB", driver);
        Assert.That(selected.Database!.Type, Is.EqualTo(typeof(FirstDatabase)));
        Assert.Throws<InvalidOperationException>(() => AttributeProviderModule.SelectMapping(mappings, "Analytics for Everyone", driver));
    }

    private static MapperInfo[] Discover() => AttributeProviderModule.Discover(new Probe());

    private sealed class Probe : ITypesProbe
    {
        public IEnumerable<Type> Locate() => [typeof(FirstDatabase), typeof(SecondDatabase), typeof(FirstMapper),
            typeof(SecondMapper), typeof(FirstLocator), typeof(SecondLocator), typeof(OdbcConnectivity), typeof(OdbcMapper)];
    }

    [Database<AnsiDialect>("Analytics for Everyone", ["first-db"], DatabaseCategory.Analytics)]
    public sealed class FirstDatabase : IDatabase;

    [Database<AnsiDialect>("Analytics for Everyone", ["second-db"], DatabaseCategory.Analytics)]
    public sealed class SecondDatabase : IDatabase;

    [Mapper<FirstDatabase, NamedParametrizer>("Identity.Driver")]
    public sealed class FirstMapper;

    [Mapper<SecondDatabase, NamedParametrizer>("Identity.Driver")]
    public sealed class SecondMapper;

    [TestLocator(typeof(FirstDatabase))]
    public sealed class FirstLocator;

    [TestLocator(typeof(SecondDatabase))]
    public sealed class SecondLocator;

    private sealed class TestLocatorAttribute(Type database) : LocatorAttribute("", [], typeof(OdbcMapper), database);
}
