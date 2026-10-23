using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal sealed class MySqlSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static MySqlSchemaSqlRenderer Default { get; } = new();

    public override string RenderIdentity(IDialect dialect, string type, long seed, long increment)
        => seed == 1 && increment == 1 ? $"{type} AUTO_INCREMENT"
            : throw new NotSupportedException($"Dialect '{dialect.GetType().Name}' cannot represent per-column identity seed or increment values.");

    public override string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
        => RenderGeneratedColumn(type, expression, stored);

    public override string RenderRegex(IDialect dialect, string expression, string pattern)
        => $"{expression} REGEXP {pattern}";

    public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
    {
        RejectQualifier(catalog, "Catalog", dialect);
        RejectQualifier(schema, "Schema", dialect);
        return QuoteIdentifierParts(dialect, database, name);
    }
}
