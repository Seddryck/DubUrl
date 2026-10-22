using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.SqlServer.Schema.Generation;

internal sealed class TSqlSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static TSqlSchemaSqlRenderer Default { get; } = new();

    public override string RenderIdentity(IDialect dialect, string type, long seed, long increment)
        => $"{type} IDENTITY({seed}, {increment})";

    public override string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
        => $"AS ({expression}){(stored ? " PERSISTED" : string.Empty)}";

    public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
        => QuoteIdentifierParts(dialect, catalog, database, schema, name);
}
