using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Snowflake.Schema.Generation;

internal sealed class SnowflakeSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static SnowflakeSchemaSqlRenderer Default { get; } = new();
    public override bool SupportsCommentRendering => true;

    public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
    {
        RejectQualifier(catalog, "Catalog", dialect);
        return QuoteIdentifierParts(dialect, database, schema, name);
    }

    public override string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
        => base.RenderComment(dialect, table, column, description.Replace("\\", "\\\\"));
}
