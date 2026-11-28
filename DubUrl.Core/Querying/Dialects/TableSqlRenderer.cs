namespace DubUrl.Querying.Dialects;

/// <summary>Renders operations on a table or view. Inputs are already-qualified SQL identifiers.</summary>
public class TableSqlRenderer
{
    internal static TableSqlRenderer Default { get; } = new();

    public virtual string RenderSelectAll(string table) => $"SELECT * FROM {table}";

    public virtual string RenderExists(string table) => $"SELECT 1 FROM {table} WHERE 1 = 0";

    public virtual string RenderHasRows(string table) => throw Unsupported("HasRows");

    public virtual string RenderCountRows(string table) => throw Unsupported("CountRows");

    public virtual string RenderTruncate(string table) => throw Unsupported("Truncate");

    /// <summary>Identifies a definitive missing-target error specific to this provider.</summary>
    /// <remarks>Core already recognizes <see cref="DubUrlTableNotFoundException"/>.</remarks>
    public virtual bool IsMissingTable(Exception error) => false;

    private static NotSupportedException Unsupported(string operation)
        => new($"{operation} is not supported by the selected database dialect.");
}
