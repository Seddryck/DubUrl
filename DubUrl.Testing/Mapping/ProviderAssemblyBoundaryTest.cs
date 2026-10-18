using DubUrl.Core.Providers;
using DubUrl.Connector.PostgreSql.Npgsql;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Providers;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Testing.Mapping;

public class ProviderAssemblyBoundaryTest
{
    [Test]
    public void CoreCatalog_ResolvesFakeProvider_WithoutBuiltInAssembly()
    {
        var descriptor = new ProviderDescriptor("Fake database", "mapping", ["fake"]);
        var catalog = new ProviderCatalog([descriptor]);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.TryGetByScheme("FAKE", out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(descriptor));
            Assert.That(resolved!.Source, Is.EqualTo("mapping"));
            Assert.That(typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Does.Not.Contain("DubUrl.Providers"));
        });
    }

    [Test]
    public void AssemblyDependencies_AreAcyclic()
    {
        var providerReferences = typeof(BuiltInProviderCatalog).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(providerReferences, Does.Contain("DubUrl.Core"));
            Assert.That(typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Does.Not.Contain("DubUrl.Providers"));
        });
    }

    [Test]
    public void Core_DoesNotReferenceCompatibilityProvidersOrDrivers()
    {
        var references = typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(references, Does.Not.Contain("DubUrl"));
            Assert.That(references, Has.None.StartsWith("DubUrl.Provider."));
            Assert.That(references, Does.Not.Contain("Npgsql"));
            Assert.That(references, Does.Not.Contain("Microsoft.Data.SqlClient"));
            Assert.That(references, Does.Not.Contain("Microsoft.Data.Sqlite"));
            Assert.That(references, Does.Not.Contain("MySqlConnector"));
        });
    }

    [Test]
    public void ProviderImplementations_AreOwnedByTheirFamilyAssemblies()
    {
        var implementations = new Dictionary<Type, string>
        {
            [typeof(MsAccessDialect)] = "DubUrl.Provider.Access",
            [typeof(Db2Dialect)] = "DubUrl.Provider.Db2",
            [typeof(DrillDialect)] = "DubUrl.Provider.Drill",
            [typeof(DuckDbDialect)] = "DubUrl.Provider.DuckDb",
            [typeof(MsExcelDialect)] = "DubUrl.Provider.Excel",
            [typeof(FirebirdSqlDialect)] = "DubUrl.Provider.FirebirdSql",
            [typeof(MySqlDialect)] = "DubUrl.Provider.MySql",
            [typeof(BuiltInDriverLocatorFactory)] = "DubUrl.Provider.Odbc",
            [typeof(OracleDialect)] = "DubUrl.Provider.Oracle",
            [typeof(PgsqlDialect)] = "DubUrl.Provider.PostgreSql",
            [typeof(SnowflakeDialect)] = "DubUrl.Provider.Snowflake",
            [typeof(SqliteDialect)] = "DubUrl.Provider.Sqlite",
            [typeof(TSqlDialect)] = "DubUrl.Provider.SqlServer",
            [typeof(TeradataDialect)] = "DubUrl.Provider.Teradata",
            [typeof(TextDialect)] = "DubUrl.Provider.Text",
            [typeof(TrinoDialect)] = "DubUrl.Provider.Trino"
        };

        Assert.Multiple(() =>
        {
            foreach (var (type, assemblyName) in implementations)
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(assemblyName), type.FullName);
        });
    }

    [Test]
    public void PostgreSqlConnector_UsesCoreFamilyProviderAndDriverOnly()
    {
        var references = typeof(PostgreSqlNpgsqlProviderModule).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(references, Does.Contain("DubUrl.Core"));
            Assert.That(references, Does.Contain("DubUrl.Provider.PostgreSql"));
            Assert.That(references, Does.Contain("Npgsql"));
            Assert.That(references, Does.Not.Contain("DubUrl"));
        });
    }

    [Test]
    public void BuiltInCatalog_DescribesMappingsWithoutClaimingTheyAreBundledOrListed()
    {
        var providers = BuiltInProviderCatalog.Create().Providers;

        Assert.Multiple(() =>
        {
            Assert.That(providers, Is.Not.Empty);
            Assert.That(providers.Select(provider => provider.Source), Is.All.EqualTo("mapping"));
        });
    }
}
