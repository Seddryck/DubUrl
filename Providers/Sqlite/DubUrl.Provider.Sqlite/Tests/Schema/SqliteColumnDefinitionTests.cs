using DubUrl.Querying.Dialects;
using DubUrl.Provider.Sqlite.Schema.Generation;
using DubUrl.Provider.Sqlite.Sql;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests.Schema;

public class SqliteColumnDefinitionTests
{
    private static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SqliteDialect>(["sqlite"]);
        return builder.Build().Get<SqliteDialect>();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IdentityPlan_ConsumesGenerationKeyAndNullability(bool tableKey)
    {
        var context = new ColumnDefinitionContext(new("items"), "id", "INTEGER", ["id"], "pk_items", tableKey,
            false, true, false, new(true));
        var plan = SqliteSchemaSqlRenderer.Default.RenderColumnDefinition(Dialect(), context);
        Assert.Multiple(() =>
        {
            Assert.That(plan!.Specification, Does.StartWith("INTEGER CONSTRAINT ").And.EndWith(" PRIMARY KEY AUTOINCREMENT"));
            Assert.That(plan.Clauses, Is.EqualTo(ColumnDefinitionClauses.Generation | ColumnDefinitionClauses.PrimaryKey | ColumnDefinitionClauses.Nullability));
        });
    }

    [TestCase("TEXT", 1, 1, false)]
    [TestCase("BIGINT", 1, 1, false)]
    [TestCase("INTEGER", 10, 1, false)]
    [TestCase("INTEGER", 1, 2, false)]
    [TestCase("INTEGER", 1, 1, true)]
    public void IdentityPlan_RejectsUnsupportedDeclarations(string type, long seed, long increment, bool nullable)
    {
        var context = new ColumnDefinitionContext(new("items"), "id", type, ["id"], null, false,
            nullable, false, false, new(true, seed, increment));
        Assert.That(() => SqliteSchemaSqlRenderer.Default.RenderColumnDefinition(Dialect(), context), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void OrdinaryColumns_KeepExistingRendering()
    {
        var context = new ColumnDefinitionContext(new("items"), "id", "INTEGER", [], null, false, false, false, false, null);
        Assert.That(SqliteSchemaSqlRenderer.Default.RenderColumnDefinition(Dialect(), context), Is.Null);
    }
}
