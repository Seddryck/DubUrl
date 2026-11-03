using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Oracle.Schema.Generation;

internal sealed class OracleSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static OracleSchemaSqlRenderer Default { get; } = new();

    public override string RenderColumnType(IDialect dialect, string type, int? length, int precision, int scale, bool nativeType)
    {
        if (!nativeType || !(type.Equals("CHAR", StringComparison.OrdinalIgnoreCase)
            || type.Equals("VARCHAR", StringComparison.OrdinalIgnoreCase)
            || type.Equals("VARCHAR2", StringComparison.OrdinalIgnoreCase)))
            return base.RenderColumnType(dialect, type, length, precision, scale, nativeType);

        // VARCHAR is currently an Oracle synonym; emit the stable native spelling.
        type = type.Equals("CHAR", StringComparison.OrdinalIgnoreCase) ? "CHAR" : "VARCHAR2";
        // Use the portable STANDARD limit. Larger VARCHAR2 values require MAX_STRING_SIZE=EXTENDED.
        var maximum = type == "CHAR" ? 2000 : 4000;
        if (length is null or <= 0 || length > maximum || precision != 0 || scale != 0)
            throw new ArgumentException($"Oracle native {type} requires a length from 1 to {maximum} without numeric precision or scale.");
        return $"{type}({length.Value})";
    }

    public override string RenderRegex(IDialect dialect, string expression, string pattern)
        => $"REGEXP_LIKE({expression}, {pattern})";
    public override bool SupportsCommentRendering => true;

    public override IReadOnlyList<SchemaSqlCommand> PlanCommands(IDialect dialect, IReadOnlyList<SchemaSqlCommand> statements)
        => statements.Select(statement => statement.IsProceduralBlock ? statement
            : statement with { CommandText = RemoveSqlTerminator(statement.CommandText) }).ToArray();

    // Only the known outer SQL terminator is removed. Literals, comments and block contents are never split.
    private static string RemoveSqlTerminator(string sql)
    {
        var text = sql.TrimEnd();
        return text.EndsWith(';') ? text[..^1] : text;
    }
}
