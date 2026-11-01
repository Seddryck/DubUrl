using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Sqlite.Schema.Generation;

internal sealed class SqliteSchemaSqlRenderer : StandardSchemaSqlRenderer, IColumnDefinitionSqlRenderer
{
    internal static SqliteSchemaSqlRenderer Default { get; } = new();

    public ColumnDefinitionPlan? RenderColumnDefinition(IDialect dialect, ColumnDefinitionContext column)
    {
        if (column.Generation is not { Identity: true } identity) return null;
        if (identity.Seed != 1 || identity.Increment != 1)
            throw new NotSupportedException("SQLite identity columns require seed 1 and increment 1.");
        if (column.Table is null || column.PrimaryKeyColumns.Count != 1 || column.PrimaryKeyColumns[0] != column.Column)
            throw new NotSupportedException("SQLite identity columns require a single-column primary key on the identity column.");
        if (!string.Equals(column.Type, "INTEGER", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("SQLite identity columns require the exact INTEGER SQL type.");
        if (column.Nullable)
            throw new NotSupportedException("SQLite identity primary keys cannot be explicitly nullable.");
        var keyName = column.PrimaryKeyName is null ? string.Empty
            : $"CONSTRAINT {dialect.Renderer.Render(column.PrimaryKeyName, "identity")} ";
        return new($"INTEGER {keyName}PRIMARY KEY AUTOINCREMENT",
            ColumnDefinitionClauses.Generation | ColumnDefinitionClauses.PrimaryKey | ColumnDefinitionClauses.Nullability);
    }

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
