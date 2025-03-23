using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class TableBuilder : ITableColumnCollectionBuilder, ITableConstraintCollectionBuilder, ITableBuilder
{
    private string? Name { get; set; }
    private ColumnCollectionBuilder Columns { get; set; } = [];
    private ConstraintCollectionBuilder Constraints { get; set; } = [];

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

    public ITableBuilder WithConstraints(Func<ConstraintCollectionBuilder, ConstraintCollectionBuilder> constraints)
    {
        Constraints = constraints(Constraints);
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

        return new Table(Name, columns, constraints.ToArray());
    }
}
