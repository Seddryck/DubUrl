using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public class IndexBuilder : IIndexTableBuilder, IIndexColumnCollectionBuilder, IIndexBuilder
{
    private string? Name { get; set; }
    private string? TableName { get; set; }
    private IndexColumnCollectionBuilder Columns { get; set; } = [];

    public IIndexTableBuilder WithName(string name)
    {
        Name = name;
        return this;
    }

    public IIndexColumnCollectionBuilder OnTable(string name)
    {
        TableName = name;
        return this;
    }

    public IIndexBuilder WithColumns(Func<IndexColumnCollectionBuilder, IndexColumnCollectionBuilder> columns)
    {
        Columns = columns(Columns);
        return this;
    }

    public Index Build()
    {
        if (Name is null)
            throw new ArgumentNullException(nameof(Name));
        if (TableName is null)
            throw new ArgumentNullException(nameof(TableName));
        var columns = Columns.Select(c => c.Build()).ToArray();

        if (columns.GroupBy(c => c.Name).Count() != columns.Length)
            throw new InvalidOperationException("Column names must be unique.");

        return new Index(Name, TableName, columns);
    }
}
