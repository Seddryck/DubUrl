namespace DubUrl.Schema.Constraints;

public sealed class ForeignKeyConstraint : Constraint
{
    public IReadOnlyList<string> SourceColumns { get; }
    public string TargetTableName { get; }
    public IReadOnlyList<string> TargetColumns { get; }

    public ForeignKeyConstraint(
        string name,
        IEnumerable<string> sourceColumns,
        string targetTableName,
        IEnumerable<string> targetColumns)
        : base(string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A foreign-key constraint name must be provided.", nameof(name))
            : name)
    {
        SourceColumns = ValidateColumns(sourceColumns, nameof(sourceColumns));
        TargetTableName = string.IsNullOrWhiteSpace(targetTableName)
            ? throw new ArgumentException("A target table name must be provided.", nameof(targetTableName))
            : targetTableName;
        TargetColumns = ValidateColumns(targetColumns, nameof(targetColumns));
        if (SourceColumns.Count != TargetColumns.Count)
            throw new ArgumentException(
                $"Foreign key '{name}' has {SourceColumns.Count} source columns but {TargetColumns.Count} target columns.",
                nameof(targetColumns));
    }

    private static string[] ValidateColumns(IEnumerable<string> columns, string parameterName)
    {
        var values = columns?.ToArray() ?? throw new ArgumentNullException(parameterName);
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty column name must be provided.", parameterName);
        if (values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Foreign-key column names must be unique.", parameterName);
        return values;
    }
}
