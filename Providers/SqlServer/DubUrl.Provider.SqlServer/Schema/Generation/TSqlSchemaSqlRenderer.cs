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

    public override string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
    {
        if (description.Length > 3750)
            throw new ArgumentException("SQL Server extended-property descriptions cannot exceed 7,500 bytes (3,750 UTF-16 code units).", nameof(description));
        if (table.Catalog is not null && table.Database is null)
            throw new NotSupportedException("SQL Server comments qualified by a catalog also require a database name.");
        // A separate batch lets each comment resolve an omitted schema without leaking or redeclaring variables.
        // Running sp_executesql in the target database also resolves its default schema in the right context.
        var scope = table.Catalog is null && table.Database is null ? string.Empty
            : string.Join('.', new[] { table.Catalog, table.Database }.Where(name => name is not null)
                .Select(name => $"[{name!.Replace("]", "]]")}]")) + ".";
        var statement = "DECLARE @schema sysname = COALESCE(@schemaName, SCHEMA_NAME()); "
            + "EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = @description, "
            + "@level0type = N'SCHEMA', @level0name = @schema, @level1type = N'TABLE', @level1name = @tableName"
            + (column is null ? ";" : ", @level2type = N'COLUMN', @level2name = @columnName;");
        var parameters = "@schemaName sysname, @tableName sysname, @description nvarchar(3750)"
            + (column is null ? string.Empty : ", @columnName sysname");
        return $"EXEC {scope}sys.sp_executesql {UnicodeLiteral(statement)}, {UnicodeLiteral(parameters)}, "
            + $"@schemaName = {(table.Schema is null ? "NULL" : UnicodeLiteral(table.Schema))}, "
            + $"@tableName = {UnicodeLiteral(table.Name)}, @description = {UnicodeLiteral(description)}"
            + (column is null ? ";" : $", @columnName = {UnicodeLiteral(column)};");
    }

    private static string UnicodeLiteral(string value) => $"N'{value.Replace("'", "''")}'";
}
