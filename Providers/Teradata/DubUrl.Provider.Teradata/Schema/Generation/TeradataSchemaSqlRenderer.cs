using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Teradata.Schema.Generation;

internal sealed class TeradataSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    internal static TeradataSchemaSqlRenderer Default { get; } = new();
    public override void ValidateKeyColumn(IDialect dialect, string type, string table, string column)
    {
        if (type.StartsWith("CLOB", StringComparison.Ordinal) || type.StartsWith("BLOB", StringComparison.Ordinal))
            throw new ArgumentException($"Teradata key column '{table}.{column}' uses {type}; supply a bounded VARCHAR/CHAR type and length.");
    }

    public override string RenderColumnType(IDialect dialect, string type, int? length, int precision, int scale, bool nativeType)
    {
        if (type is "VARCHAR" or "CHAR")
        {
            if (length is null or -1)
                throw new ArgumentException($"Teradata {type} requires an explicit bounded length; use an explicit CLOB native type for large non-key text.");
        }
        return base.RenderColumnType(dialect, type, length, precision, scale, nativeType);
    }
}
