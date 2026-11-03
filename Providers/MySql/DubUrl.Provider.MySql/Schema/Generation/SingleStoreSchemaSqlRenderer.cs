using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal sealed class SingleStoreSchemaSqlRenderer : StandardSchemaSqlRenderer, ITableDescriptionSqlRenderer
{
    internal static SingleStoreSchemaSqlRenderer Default { get; } = new();
    private SingleStoreSchemaSqlRenderer() : base(MySqlSchemaSqlRenderer.Default) { }

    public string RenderTableDescription(IDialect dialect, SchemaSqlObjectName table, string description)
    {
        _ = RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        return " COMMENT = '" + description.Replace("\\", "\\\\").Replace("'", "''") + "'";
    }

    public override string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
        => column is null
            ? throw new NotSupportedException("SingleStore table descriptions require ITableDescriptionSqlRenderer during CREATE TABLE; ALTER TABLE COMMENT does not persist them on the supported engine.")
            : string.Empty;
}
