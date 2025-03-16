using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class TableBuilder : ITableColumnCollectionBuilder, ITableBuilder
{
    private string? Name { get; set; }
    private ColumnCollectionBuilder Columns { get; set; } = new();

    public ITableColumnCollectionBuilder WithName(string name)
    {
        Name = name;
        return this;
    }

    public ITableBuilder WithColumns(Func<ColumnCollectionBuilder, ColumnCollectionBuilder> columns)
    {
        Columns = columns(new());
        return this;
    }

    public Table Build()
    {
        if (Name is null)
            throw new ArgumentNullException(nameof(Name));
        var columns = Columns.Select(c => c.Build()).ToArray();

        if (columns.GroupBy(c => c.Name).Count() != columns.Length)
            throw new InvalidOperationException("Column names must be unique.");

        return new Table(Name, columns);
    }
}
