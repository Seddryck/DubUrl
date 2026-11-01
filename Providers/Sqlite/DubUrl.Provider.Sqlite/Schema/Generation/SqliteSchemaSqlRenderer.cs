using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Sqlite.Schema.Generation;

internal sealed class SqliteSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static SqliteSchemaSqlRenderer Default { get; } = new();

    public override bool RenderForeignKeysInline(IDialect dialect) => true;

    public override string RenderIdentity(IDialect dialect, string type, long seed, long increment)
        => throw new NotSupportedException("SQLite auto-increment requires the table-specific INTEGER PRIMARY KEY AUTOINCREMENT form and cannot represent a provider-neutral identity column.");

    public override string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
        => RenderGeneratedColumn(type, expression, stored);

    public override string RenderCreateIndexName(IDialect dialect, SchemaSqlObjectName index, SchemaSqlObjectName table)
    {
        ValidateSameDatabase(index, table);
        return RenderObjectName(dialect, index.Catalog, index.Database ?? table.Database, index.Schema, index.Name);
    }

    public override string RenderIndexTableName(IDialect dialect, SchemaSqlObjectName table)
    {
        RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        return QuoteNames(table.Name);
    }

    public override string RenderForeignKeyTarget(IDialect dialect, SchemaSqlObjectName table, SchemaSqlObjectName target)
    {
        ValidateSameDatabase(target, table);
        RenderObjectName(dialect, target.Catalog, target.Database, target.Schema, target.Name);
        return QuoteNames(target.Name);
    }

    public override string RenderIdentifier(IDialect dialect, string name) => QuoteNames(name);

    private static string QuoteNames(params string?[] parts)
        => string.Join('.', parts.Where(part => part is not null).Select(part => "\"" + part!.Replace("\"", "\"\"") + "\""));

    private static void ValidateSameDatabase(SchemaSqlObjectName name, SchemaSqlObjectName table)
    {
        if (name.Database is not null && name.Database != (table.Database ?? "main"))
            throw new NotSupportedException("SQLite indexes and foreign keys must belong to the same database as their table.");
    }

    public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
    {
        RejectQualifier(catalog, "Catalog", dialect);
        RejectQualifier(schema, "Schema", dialect);
        return QuoteNames(database, name);
    }
}
