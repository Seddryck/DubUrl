using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public sealed class ForeignKeyConstraintBuilder : IConstraintBuilder
{
    private string? Name { get; set; }
    private string[]? SourceColumns { get; set; }
    private DatabaseObjectName? TargetTable { get; set; }
    private string[]? TargetColumns { get; set; }

    public ForeignKeyConstraintBuilder WithName(string name)
    {
        Name = name;
        return this;
    }

    public ForeignKeyConstraintBuilder FromColumns(params string[] names)
    {
        SourceColumns = names;
        return this;
    }

    public ForeignKeyConstraintBuilder FromColumn(string name)
        => FromColumns(name);

    public ForeignKeyConstraintBuilder References(string tableName, params string[] columnNames)
        => References(new DatabaseObjectName(tableName), columnNames);

    public ForeignKeyConstraintBuilder References(DatabaseObjectName table, params string[] columnNames)
    {
        TargetTable = table;
        TargetColumns = columnNames;
        return this;
    }

    Constraint IConstraintBuilder.Build()
        => new ForeignKeyConstraint(
            Name ?? throw new InvalidOperationException("A foreign-key constraint name must be provided."),
            SourceColumns ?? throw new InvalidOperationException("Source columns must be provided."),
            TargetTable ?? throw new InvalidOperationException("A target table must be provided."),
            TargetColumns ?? throw new InvalidOperationException("Target columns must be provided."));
}
