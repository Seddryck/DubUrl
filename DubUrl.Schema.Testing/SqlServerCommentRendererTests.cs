using System.Data;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Schema.Testing;

public class SqlServerCommentRendererTests
{
    private IDialect dialect = null!;

    [SetUp]
    public void SetUp()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql"]);
        dialect = builder.Build().Get<TSqlDialect>();
    }

    [Test]
    public void Descriptions_RenderExtendedPropertiesAfterTables_WithUnicodeAndEscaping()
    {
        var table = new Table(new DatabaseObjectName("Order's.表", schema: "sales'部"),
            [new Column("Name's.列", DbType.String, description: "Owner's name\nPréféré 雪")],
            description: "Customer's record");
        var renderer = new SchemaScriptRenderer(dialect);
        var sql = renderer.Render(new Schema([table], []));

        Assert.Multiple(() =>
        {
            Assert.That(renderer.SupportsComments, Is.True);
            Assert.That(sql, Does.Not.Contain("COMMENT ON"));
            Assert.That(sql.IndexOf("EXEC sys.sp_executesql", StringComparison.Ordinal),
                Is.GreaterThan(sql.IndexOf("CREATE TABLE", StringComparison.Ordinal)));
            Assert.That(sql.Split("EXEC sys.sp_executesql"), Has.Length.EqualTo(3));
            Assert.That(sql, Does.Contain("EXEC sys.sp_addextendedproperty @name = N''MS_Description''"));
            Assert.That(sql, Does.Contain("@level0type = N''SCHEMA'', @level0name = @schema"));
            Assert.That(sql, Does.Contain("@level1type = N''TABLE'', @level1name = @tableName"));
            Assert.That(sql, Does.Contain("@level2type = N''COLUMN'', @level2name = @columnName"));
            Assert.That(sql, Does.Contain("@schemaName = N'sales''部', @tableName = N'Order''s.表'"));
            Assert.That(sql, Does.Contain("@description = N'Customer''s record';"));
            Assert.That(sql, Does.Contain("@description = N'Owner''s name\r\nPréféré 雪', @columnName = N'Name''s.列';"));
        });
    }

    [Test]
    public void UnqualifiedTables_ResolveDefaultSchema_InSeparateBatches()
    {
        var tables = new[] { "First", "Second" }.Select(name => new Table(name,
            [new Column("Id", DbType.Int32, description: "Identifier")], description: "Description"));
        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([.. tables], []));

        Assert.Multiple(() =>
        {
            Assert.That(sql.Split("EXEC sys.sp_executesql"), Has.Length.EqualTo(5));
            Assert.That(sql, Does.Contain("DECLARE @schema sysname = COALESCE(@schemaName, SCHEMA_NAME());"));
            Assert.That(sql, Does.Contain("@schemaName = NULL"));
            Assert.That(sql, Does.Not.Contain("N'dbo'"));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\n")]
    public void AbsentDescriptions_EmitNoExtendedProperties(string? description)
    {
        var table = new Table("Customer", [new Column("Id", DbType.Int32, description: description)], description: description);
        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([table], []));
        Assert.That(sql, Does.Not.Contain("sp_executesql"));
    }

    [TestCase(null, "store]db", "EXEC [store]]db].sys.sp_executesql")]
    [TestCase("server", "store", "EXEC [server].[store].sys.sp_executesql")]
    public void QualifiedTables_ExecuteInTargetDatabase(string? catalog, string database, string expected)
    {
        var sql = dialect.SchemaSqlRenderer.RenderComment(dialect,
            new("Customer", "sales", database, catalog), null, "Description");
        Assert.That(sql, Does.StartWith(expected));
    }

    [Test]
    public void CatalogWithoutDatabase_IsRejected()
        => Assert.That(() => dialect.SchemaSqlRenderer.RenderComment(dialect,
            new("Customer", Catalog: "server"), null, "Description"), Throws.TypeOf<NotSupportedException>());

    [Test]
    public void ExtendedPropertySizeLimit_DoesNotSilentlyTruncate()
    {
        Assert.That(dialect.SchemaSqlRenderer.RenderComment(dialect, new("Customer"), null, new string('雪', 3750)),
            Does.Contain(new string('雪', 3750)));
        Assert.That(() => dialect.SchemaSqlRenderer.RenderComment(dialect, new("Customer"), null, new string('雪', 3751)),
            Throws.ArgumentException.With.Message.Contains("7,500 bytes"));
    }

    [TestCase("pgsql")]
    [TestCase("cockroach")]
    [TestCase("cratedb")]
    [TestCase("duckdb")]
    public void ExistingProviders_PreserveCommentOnSyntax(string alias)
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<PgsqlDialect>(["pgsql"]);
        builder.AddDialect<CockRoachDialect>(["cockroach"]);
        builder.AddDialect<CrateDbDialect>(["cratedb"]);
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        var provider = builder.Build().Get(alias);
        var table = new Table(new DatabaseObjectName("Customer", schema: "sales"),
            [new Column("Id", DbType.Int32, description: "Owner's ID")], description: "Customers");
        var sql = new SchemaScriptRenderer(provider).Render(new Schema([table], []));
        var name = provider.SchemaSqlRenderer.RenderObjectName(provider, null, null, "sales", "Customer");

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain($"COMMENT ON TABLE {name} IS 'Customers';"));
            Assert.That(sql, Does.Contain($"COMMENT ON COLUMN {name}.{provider.Renderer.Render("Id", "identity")} IS 'Owner''s ID';"));
        });
    }
}
