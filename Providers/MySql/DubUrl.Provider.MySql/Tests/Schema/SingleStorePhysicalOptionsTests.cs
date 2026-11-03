using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Schema;

public class SingleStorePhysicalOptionsTests
{
    [Test]
    public void RowstorePlanConsumesKeysAndPreservesQuotedNames()
    {
        var dialect = SingleStoreSchemaTests.Dialect();
        var renderer = (ITableDefinitionSqlRenderer)dialect.SchemaSqlRenderer;
        var context = new TableDefinitionContext(new("items", Database: "shop"), ["tenant", "code", "region"],
            UniqueKeys: [new("uq_code", ["tenant", "code"]), new("uq_region", ["tenant", "region"])],
            Options: new(SchemaTableStorage.Rowstore, ["tenant"]));
        var plan = renderer.RenderTableDefinition(dialect, context);
        Assert.That(plan.CreatePrefix, Is.EqualTo("CREATE ROWSTORE TABLE"));
        Assert.That(plan.Definitions, Is.EqualTo(new[] { "UNIQUE KEY `uq_code` (`tenant`, `code`)", "UNIQUE KEY `uq_region` (`tenant`, `region`)", "SHARD KEY (`tenant`)" }));
        var index = renderer.RenderCreateIndex(dialect, new(new("ix_code"), context, [new("tenant", false), new("code", true)]));
        Assert.That(index, Is.EqualTo("CREATE INDEX `ix_code` ON `shop`.`items` (`tenant` ASC, `code` DESC) USING BTREE;"));
    }

    [Test]
    public void ColumnstorePlanPreservesHashUniquenessAndSortDirection()
    {
        var dialect = SingleStoreSchemaTests.Dialect();
        var renderer = (ITableDefinitionSqlRenderer)dialect.SchemaSqlRenderer;
        var context = new TableDefinitionContext(new("items"), ["id", "code"], new(null, ["id"]),
            Options: new(SchemaTableStorage.Columnstore, ["id"], [new("code", true)]));
        var plan = renderer.RenderTableDefinition(dialect, context);
        Assert.That(plan.Definitions, Is.EqualTo(new[] { "PRIMARY KEY (`id`) USING HASH", "SHARD KEY (`id`)", "SORT KEY (`code` DESC)" }));
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("ix"), context, [new("code", false)])), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("ix"), context, [new("code")], Structure: SchemaIndexStructure.Ordered)), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void IncompatibleShardUniqueStorageAndColumnsAreRejected()
    {
        var dialect = SingleStoreSchemaTests.Dialect();
        var renderer = (ITableDefinitionSqlRenderer)dialect.SchemaSqlRenderer;
        var context = new TableDefinitionContext(new("items"), ["id", "code"], new(null, ["id"]), Options: new(SchemaTableStorage.Rowstore, ["code"]));
        Assert.That(() => renderer.RenderTableDefinition(dialect, context), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderTableDefinition(dialect, context with { Options = new(ShardKey: []) }), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderTableDefinition(dialect, context with { Options = new(ShardKey: ["missing"]) }), Throws.ArgumentException);
        Assert.That(() => renderer.RenderTableDefinition(dialect, context with { Options = new(SchemaTableStorage.Rowstore, ["id"], []) }), Throws.TypeOf<NotSupportedException>());
        var valid = context with { Options = new(SchemaTableStorage.Rowstore, ["id"]) };
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("ix"), valid, [new("code")], Unique: true)), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("ix", Database: "other"), valid, [new("code")])), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("ix"), valid, [new("code", true)], Structure: SchemaIndexStructure.Hash)), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => renderer.RenderTableDefinition(dialect, valid with { Options = new((SchemaTableStorage)99) }), Throws.ArgumentException);
    }
    [Test]
    public void UniqueIndexesAreInlineAndCannotBeAddedOnline()
    {
        var dialect = SingleStoreSchemaTests.Dialect();
        var renderer = (ITableDefinitionSqlRenderer)dialect.SchemaSqlRenderer;
        var table = new TableDefinitionContext(new("items"), ["id", "code"],
            Options: new(SchemaTableStorage.Rowstore, ["id"]),
            Indexes: [new("uq_code", [new("id"), new("code", true)], Unique: true)]);
        Assert.That(renderer.RenderTableDefinition(dialect, table).Definitions,
            Does.Contain("UNIQUE KEY `uq_code` (`id`, `code` DESC) USING BTREE"));
        Assert.That(() => renderer.RenderCreateIndex(dialect, new(new("uq_code"), table, [new("id"), new("code")], Unique: true)),
            Throws.TypeOf<NotSupportedException>().With.Message.Contains("CREATE TABLE"));
    }
}
