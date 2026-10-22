using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Schema.Testing;

public class ProviderSchemaSqlRendererTests
{
    private IDialectRegistry registry = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql"]);
        builder.AddDialect<PgsqlDialect>(["pgsql"]);
        builder.AddDialect<CockRoachDialect>(["cockroach"]);
        builder.AddDialect<CrateDbDialect>(["cratedb"]);
        builder.AddDialect<QuestDbDialect>(["questdb"]);
        builder.AddDialect<MySqlDialect>(["mysql"]);
        builder.AddDialect<SingleStoreDialect>(["singlestore"]);
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        registry = builder.Build();
    }

    [TestCase("mssql")]
    [TestCase("pgsql")]
    [TestCase("cockroach")]
    [TestCase("cratedb")]
    [TestCase("questdb")]
    [TestCase("mysql")]
    [TestCase("singlestore")]
    [TestCase("sqlite")]
    [TestCase("duckdb")]
    public void EngineRenderer_IsOwnedByItsProviderAssembly(string alias)
    {
        var dialect = registry.Get(alias);
        Assert.That(dialect.SchemaSqlRenderer.GetType().Assembly, Is.SameAs(dialect.GetType().Assembly));
        Assert.That(dialect.SchemaSqlRenderer.GetType().Assembly, Is.Not.SameAs(typeof(StandardSchemaSqlRenderer).Assembly));
    }

    [TestCase("mssql", true, "AS (7) PERSISTED")]
    [TestCase("mssql", false, "AS (7)")]
    [TestCase("pgsql", true, "INTEGER GENERATED ALWAYS AS (7) STORED")]
    [TestCase("pgsql", false, null)]
    [TestCase("cockroach", true, "INTEGER AS (7) STORED")]
    [TestCase("cockroach", false, null)]
    [TestCase("cratedb", true, null)]
    [TestCase("cratedb", false, null)]
    [TestCase("questdb", true, null)]
    [TestCase("questdb", false, null)]
    [TestCase("mysql", true, "INTEGER GENERATED ALWAYS AS (7) STORED")]
    [TestCase("mysql", false, "INTEGER GENERATED ALWAYS AS (7) VIRTUAL")]
    [TestCase("singlestore", true, "INTEGER GENERATED ALWAYS AS (7) STORED")]
    [TestCase("singlestore", false, "INTEGER GENERATED ALWAYS AS (7) VIRTUAL")]
    [TestCase("sqlite", true, "INTEGER GENERATED ALWAYS AS (7) STORED")]
    [TestCase("sqlite", false, "INTEGER GENERATED ALWAYS AS (7) VIRTUAL")]
    [TestCase("duckdb", true, null)]
    [TestCase("duckdb", false, "INTEGER GENERATED ALWAYS AS (7) VIRTUAL")]
    public void GeneratedColumns_PreserveSupportedAndRejectedForms(string alias, bool stored, string? expected)
    {
        var dialect = registry.Get(alias);
        if (expected is null)
            Assert.That(() => dialect.SchemaSqlRenderer.RenderGenerated(dialect, "INTEGER", "7", stored), Throws.TypeOf<NotSupportedException>());
        else
            Assert.That(dialect.SchemaSqlRenderer.RenderGenerated(dialect, "INTEGER", "7", stored), Is.EqualTo(expected));
        Assert.That(stored ? dialect.Capabilities.SupportsStoredGeneratedColumns : dialect.Capabilities.SupportsVirtualGeneratedColumns,
            Is.EqualTo(expected is not null));
    }

    [TestCase("mssql", null)]
    [TestCase("pgsql", "column_sql ~ 'a''b'")]
    [TestCase("cockroach", "column_sql ~ 'a''b'")]
    [TestCase("cratedb", "column_sql ~ 'a''b'")]
    [TestCase("questdb", "column_sql ~ 'a''b'")]
    [TestCase("mysql", "column_sql REGEXP 'a''b'")]
    [TestCase("singlestore", "column_sql REGEXP 'a''b'")]
    [TestCase("sqlite", null)]
    [TestCase("duckdb", "regexp_matches(column_sql, 'a''b')")]
    public void Regex_PreservesSyntaxAndAlreadyEscapedInputs(string alias, string? expected)
    {
        var dialect = registry.Get(alias);
        if (expected is null)
            Assert.That(() => dialect.SchemaSqlRenderer.RenderRegex(dialect, "column_sql", "'a''b'"), Throws.TypeOf<NotSupportedException>());
        else
            Assert.That(dialect.SchemaSqlRenderer.RenderRegex(dialect, "column_sql", "'a''b'"), Is.EqualTo(expected));
        Assert.That(dialect.Capabilities.SupportsRegularExpressionChecks, Is.EqualTo(expected is not null));
    }

    [TestCase("mssql", "server", "store", "sales", "[server].[store].[sales].[Customer]")]
    [TestCase("pgsql", null, null, "sales", "\"sales\".\"Customer\"")]
    [TestCase("cockroach", null, null, "sales", "\"sales\".\"Customer\"")]
    [TestCase("cratedb", null, null, "sales", "\"sales\".\"Customer\"")]
    [TestCase("questdb", null, null, "sales", "\"sales\".\"Customer\"")]
    [TestCase("mysql", null, "store", null, "`store`.`Customer`")]
    [TestCase("singlestore", null, "store", null, "`store`.`Customer`")]
    [TestCase("sqlite", null, "store", null, "store.Customer")]
    [TestCase("duckdb", null, "store", "sales", "store.sales.Customer")]
    public void QualifiedNames_PreserveProviderFormatting(string alias, string? catalog, string? database, string? schema, string expected)
    {
        var dialect = registry.Get(alias);
        Assert.That(dialect.SchemaSqlRenderer.RenderObjectName(dialect, catalog, database, schema, "Customer"), Is.EqualTo(expected));
    }
}
