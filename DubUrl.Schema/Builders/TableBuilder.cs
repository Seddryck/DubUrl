using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public class TableBuilder : ITableColumnCollectionBuilder, ITableConstraintCollectionBuilder, ITableBuilder
{
    private string? Name { get; set; }
    private ColumnCollectionBuilder Columns { get; set; } = [];
    private TableConstraintCollectionBuilder Constraints { get; set; } = [];
    private string? Description { get; set; }

    public ITableColumnCollectionBuilder WithName(string name)
    {
        Name = name;
        return this;
    }

    public ITableConstraintCollectionBuilder WithColumns(Func<ColumnCollectionBuilder, ColumnCollectionBuilder> columns)
    {
        Columns = columns(Columns);
        return this;
    }

    public ITableBuilder WithConstraints(Func<TableConstraintCollectionBuilder, TableConstraintCollectionBuilder> constraints)
    {
        Constraints = constraints(Constraints);
        return this;
    }

    public ITableBuilder WithDescription(string? description)
    {
        Description = description;
        return this;
    }

    public Table Build()
    {
        if (Name is null)
            throw new ArgumentNullException(nameof(Name));
        var columns = Columns.Select(c => c.Build()).ToArray();

        if (columns.GroupBy(c => c.Name).Count() != columns.Length)
            throw new InvalidOperationException("Column names must be unique.");

        var constraints = Constraints.Select(c => c.Build());

        var primaryKeyConstraint = constraints.OfType<PrimaryKeyConstraint>().SingleOrDefault();
        if (primaryKeyConstraint is not null )
        {
            foreach (var column in primaryKeyConstraint.Columns)
                if (!columns.Any(c => c.Name == column.Key))
                    throw new InvalidOperationException($"Primary key column '{column.Key}' does not exist in table '{Name}'.");
        }

        foreach (var foreignKey in constraints.OfType<ForeignKeyConstraint>())
        {
            var missingColumns = foreignKey.SourceColumns.Where(name => !columns.Any(column => column.Name == name)).ToArray();
            if (missingColumns.Length > 0)
                throw new InvalidOperationException(
                    $"Foreign key '{foreignKey.Name}' references missing source column(s) {string.Join(", ", missingColumns)} in table '{Name}'.");
        }

        var duplicateConstraintNames = constraints.Where(constraint => constraint.Name is not null)
            .GroupBy(constraint => constraint.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateConstraintNames.Length > 0)
            throw new InvalidOperationException($"Constraint names must be unique within table '{Name}': {string.Join(", ", duplicateConstraintNames)}.");

        return new Table(Name, columns, [.. constraints], Description);
    }
}
