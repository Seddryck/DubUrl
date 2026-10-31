using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal sealed class MySqlSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static MySqlSchemaSqlRenderer Default { get; } = new();

    public override bool SupportsCommentRendering => true;

    public override string RenderColumnType(IDialect dialect, string type, int? length, int precision, int scale, bool nativeType)
    {
        if (type == "TEXT" && length > 0 && !nativeType) type = "VARCHAR";
        if (type is "VARCHAR" or "CHAR")
        {
            if (length is null or -1)
                throw new ArgumentException($"MySQL {type} requires an explicit bounded length.");
        }
        if (type.Contains("TEXT") || type.Contains("BLOB"))
        {
            if (length > 0) throw new ArgumentException($"MySQL {type} does not accept a character length; use a bounded VARCHAR/CHAR native type.");
            return type;
        }
        return base.RenderColumnType(dialect, type, length, precision, scale, nativeType);
    }

    public override void ValidateKeyColumn(IDialect dialect, string type, string table, string column)
    {
        if (type.Contains("TEXT") || type.Contains("BLOB") || type == "JSON")
            throw new ArgumentException($"MySQL key column '{table}.{column}' uses {type}; supply an explicit bounded VARCHAR/CHAR type and length.");
    }

    public override string RenderColumnComment(IDialect dialect, string description)
        => " COMMENT '" + description.Replace("\\", "\\\\").Replace("'", "''") + "'";

    public override string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
    {
        var name = RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        var literal = "'" + description.Replace("\\", "\\\\").Replace("'", "''") + "'";
        // Column COMMENT clauses are emitted with the complete definition in CREATE TABLE.
        return column is null ? $"ALTER TABLE {name} COMMENT = {literal};" : string.Empty;
    }

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
