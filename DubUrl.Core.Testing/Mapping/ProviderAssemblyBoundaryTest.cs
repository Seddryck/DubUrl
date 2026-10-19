using DubUrl.Provider.Access.Sql;
using DubUrl.Provider.Db2.Sql;
using DubUrl.Provider.Drill.Sql;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.Excel.Sql;
using DubUrl.Provider.FirebirdSql.Sql;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.Snowflake.Sql;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.Teradata.Sql;
using DubUrl.Provider.Text.Sql;
using DubUrl.Provider.Trino.Sql;
using DubUrl.Core.Providers;
using DubUrl.Provider.PostgreSql.Npgsql;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Providers;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

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
            Assert.That(typeof(BuiltInProviderCatalog).Assembly, Is.SameAs(typeof(DubUrl.Mapping.BuiltInProviderModule).Assembly));
            Assert.That(typeof(BuiltInProviderCatalog).Assembly.GetName().Name, Is.EqualTo("DubUrl"));
            Assert.That(providerReferences, Does.Contain("DubUrl.Core"));
            Assert.That(providerReferences, Does.Not.Contain("DubUrl.Providers"));
            Assert.That(typeof(ProviderCatalog).Assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name), Does.Not.Contain("DubUrl"));
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

    [TestCase("Db2", "Db2Rewriter")]
    [TestCase("DuckDb", "DuckdbRewriter")]
    [TestCase("FirebirdSql", "FirebirdSqlRewriter")]
    [TestCase("MySql", "MySqlConnectorRewriter,MySqlDataRewriter,MariaDbConnectorRewriter,MariaDbDataRewriter,SingleStoreRewriter")]
    [TestCase("Odbc", "OdbcRewriter,OdbcDbqRewriter,DrillOdbcRewriter,DuckdbOdbcRewriter,QuestDbOdbcRewriter,TrinoOdbcRewriter")]
    [TestCase("Oracle", "OracleManagedDataAccessRewriter")]
    [TestCase("PostgreSql", "PostgresqlRewriter,CockRoachRewriter,CrateDbRewriter,QuestDbRewriter,TimescaleRewriter")]
    [TestCase("Snowflake", "SnowflakeRewriter")]
    [TestCase("Sqlite", "SqliteRewriter")]
    [TestCase("SqlServer", "MsSqlServerRewriter")]
    [TestCase("Teradata", "TeradataRewriter")]
    [TestCase("Trino", "TrinoRewriter")]
    public void Rewriters_RetainTheirProviderAssemblyIdentity(string family, string rewriters)
    {
        var assemblyName = $"DubUrl.Provider.{family}";
        var assembly = System.Reflection.Assembly.Load(assemblyName);
        foreach (var rewriter in rewriters.Split(','))
        {
            var fullName = $"DubUrl.Provider.{family}.Rewriting.{rewriter}";
            var type = assembly.GetType(fullName, throwOnError: true)!;
            Assert.Multiple(() =>
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(assemblyName));
                Assert.That(typeof(DubUrl.Rewriting.ConnectionStringRewriter).IsAssignableFrom(type), Is.True);
                Assert.That(typeof(ProviderCatalog).Assembly.GetType(fullName), Is.Null);
            });
        }
    }
    [Test]
    public void PostgreSqlBinding_UsesSharedRegistrationAndSelectsItsFamilyAndDriver()
    {
        var module = new ProviderModule();
        var registry = new DubUrl.Mapping.ProviderCatalogBuilder().AddModule(module).Build();
        var references = module.GetType().Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(references, Does.Contain("DubUrl.Core"));
            Assert.That(references, Does.Not.Contain("DubUrl"));
            Assert.That(module, Is.InstanceOf<DubUrl.Mapping.AssemblyMetadataProviderModule>());
            Assert.That(module.GetType().FullName, Is.EqualTo("DubUrl.Provider.PostgreSql.Npgsql.ProviderModule"));
            Assert.That(registry.GetMapper("pg").GetType().Assembly.GetName().Name, Is.EqualTo("DubUrl.Provider.PostgreSql"));
            Assert.That(registry.GetProviderFactory(["pg"]).GetType().Assembly.GetName().Name, Is.EqualTo("Npgsql"));
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
