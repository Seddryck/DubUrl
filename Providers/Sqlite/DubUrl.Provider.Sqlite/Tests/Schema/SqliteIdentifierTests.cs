using DubUrl.Provider.Sqlite.Sql.Rendering;
using DubUrl.Provider.Sqlite.Schema.Generation;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests.Schema;

public class SqliteIdentifierTests
{
    [TestCase("select")]
    [TestCase("two words")]
    [TestCase("a.b")]
    [TestCase("O'Brien")]
    [TestCase("a\"b")]
    [TestCase("[]`;--")]
    [TestCase("élève東京")]
    public void Identifier_IsOneEscapedLiteralComponent(string name)
    {
        Assert.That(new SqliteRenderer().Render(name, "identity"),
            Is.EqualTo("\"" + name.Replace("\"", "\"\"") + "\""));
    }

    [Test]
    public void ObjectName_QuotesDatabaseAndLiteralDotsIndependently()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        var dialect = builder.Build().Get<SqliteDialect>();
        Assert.That(SqliteSchemaSqlRenderer.Default.RenderObjectName(dialect, null, "db.name", null, "a.\"b"),
            Is.EqualTo("\"db.name\".\"a.\"\"b\""));
    }
}
