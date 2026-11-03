using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Schema;

public class SingleStoreSchemaTests
{
    internal static IDialect Dialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SingleStoreDialect>(["singlestore"]);
        return builder.Build().Get<SingleStoreDialect>();
    }

    [TestCase("CHAR")]
    [TestCase("VARCHAR")]
    public void NativeCharacterKeysPreserveBoundedDimensions(string type)
    {
        var dialect = Dialect();
        Assert.That(dialect.Capabilities.NativeTypes.Contains(type), Is.True);
        var rendered = dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, 32, 0, 0, true);
        Assert.That(rendered, Is.EqualTo(type + "(32)"));
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, type.ToLowerInvariant(), 32, 0, 0, true), Is.EqualTo(rendered));
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, 0, 0, 0, true), Throws.ArgumentException);
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, 32, 2, 1, true), Throws.ArgumentException);
        Assert.That(() => dialect.SchemaSqlRenderer.ValidateKeyColumn(dialect, rendered, "items", "code"), Throws.Nothing);
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, null, 0, 0, true), Throws.ArgumentException);
        Assert.That(() => dialect.SchemaSqlRenderer.RenderColumnType(dialect, type, -1, 0, 0, true), Throws.ArgumentException);
    }

    [Test]
    public void LogicalBoundedTextAndUnboundedKeyRestrictionsRemain()
    {
        var dialect = Dialect();
        Assert.That(dialect.SchemaSqlRenderer.RenderColumnType(dialect, "TEXT", 12, 0, 0, false), Is.EqualTo("VARCHAR(12)"));
        Assert.That(() => dialect.SchemaSqlRenderer.ValidateKeyColumn(dialect, "TEXT", "items", "code"), Throws.ArgumentException);
        Assert.That(dialect.Capabilities.NativeTypes.Contains("VARCHAR(32); DROP TABLE items"), Is.False);
    }
    [Test]
    public void TableDescriptionsRequireInlineConsumerAndPreserveLiteral()
    {
        var dialect = Dialect();
        var renderer = (ITableDescriptionSqlRenderer)dialect.SchemaSqlRenderer;
        Assert.That(renderer.RenderTableDescription(dialect, new("items", Database: "store"), "O'Brien — café\r\npath\\x"),
            Is.EqualTo(" COMMENT = 'O''Brien — café\r\npath\\\\x'"));
        Assert.That(() => renderer.RenderTableDescription(dialect, new("items", Schema: "wrong"), "text"), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => dialect.SchemaSqlRenderer.RenderComment(dialect, new("items"), null, "text"), Throws.TypeOf<NotSupportedException>());
        Assert.That(dialect.SchemaSqlRenderer.RenderComment(dialect, new("items"), "code", "text"), Is.Empty);
    }
    [Test]
    public void ComputedColumnsUsePersistedSyntaxAndRejectVirtualGeneration()
    {
        var dialect = Dialect();
        Assert.That(dialect.Capabilities.SupportsStoredGeneratedColumns, Is.True);
        Assert.That(dialect.Capabilities.SupportsVirtualGeneratedColumns, Is.False);
        Assert.That(dialect.SchemaSqlRenderer.RenderGenerated(dialect, "DECIMAL(12, 3)", "`amount` * 2", true),
            Is.EqualTo("AS (`amount` * 2) PERSISTED DECIMAL(12, 3)"));
        Assert.That(() => dialect.SchemaSqlRenderer.RenderGenerated(dialect, "INT", "1", false), Throws.TypeOf<NotSupportedException>());
    }
}
