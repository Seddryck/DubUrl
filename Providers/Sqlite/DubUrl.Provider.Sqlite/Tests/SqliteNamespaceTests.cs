using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests;

public class SqliteNamespaceTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        return builder.Build().Get<SqliteDialect>();
    }

    [Test]
    public void ContextualNames_PreserveLiteralDatabaseAndObjectComponents()
    {
        var dialect = Dialect();
        var renderer = new StandardSchemaSqlRenderer(dialect.SchemaSqlRenderer);
        Assert.That(renderer.RenderIdentifier(dialect, "literal.column"), Is.EqualTo("\"literal.column\""));
        var table = new SchemaSqlObjectName("parent.table", Database: "aux.db");
        Assert.That(renderer.RenderObjectName(dialect, null, "aux.db", null, "a\"b"), Is.EqualTo("\"aux.db\".\"a\"\"b\""));
        Assert.That(renderer.RenderCreateIndexName(dialect, new("idx"), table), Is.EqualTo("\"aux.db\".\"idx\""));
        Assert.That(renderer.RenderIndexTableName(dialect, table), Is.EqualTo("\"parent.table\""));
        Assert.That(renderer.RenderForeignKeyTarget(dialect, table, table), Is.EqualTo("\"parent.table\""));
    }

    [TestCase("index")]
    [TestCase("foreign key")]
    public void CrossDatabaseRelationships_AreRejected(string kind)
    {
        var dialect = Dialect();
        var table = new SchemaSqlObjectName("child", Database: "main");
        var target = new SchemaSqlObjectName("parent", Database: "aux");
        Assert.That(() => kind == "index"
            ? dialect.SchemaSqlRenderer.RenderCreateIndexName(dialect, target, table)
            : dialect.SchemaSqlRenderer.RenderForeignKeyTarget(dialect, table, target), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void SchemaQualifiers_RemainUnsupported()
    {
        var dialect = Dialect();
        Assert.That(() => dialect.SchemaSqlRenderer.RenderObjectName(dialect, null, null, "main", "sample"),
            Throws.TypeOf<NotSupportedException>());
    }
}
