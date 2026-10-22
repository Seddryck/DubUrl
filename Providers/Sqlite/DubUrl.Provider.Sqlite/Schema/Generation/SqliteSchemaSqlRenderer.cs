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

    public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
    {
        RejectQualifier(catalog, "Catalog", dialect);
        RejectQualifier(schema, "Schema", dialect);
        return QuoteIdentifierParts(dialect, database, name);
    }
}
