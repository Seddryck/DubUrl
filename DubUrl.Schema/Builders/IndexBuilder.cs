using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public class IndexBuilder : IIndexTableBuilder, IIndexColumnCollectionBuilder, IIndexBuilder
{
    private DatabaseObjectName? Identity { get; set; }
    private DatabaseObjectName? TableIdentity { get; set; }
    private IndexColumnCollectionBuilder Columns { get; set; } = [];

    public IIndexTableBuilder WithName(string name)
    {
        Identity = new DatabaseObjectName(name);
        return this;
    }

    public IIndexTableBuilder WithIdentity(DatabaseObjectName identity)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        return this;
    }

    public IIndexColumnCollectionBuilder OnTable(string name)
        => OnTable(new DatabaseObjectName(name));

    public IIndexColumnCollectionBuilder OnTable(DatabaseObjectName identity)
    {
        TableIdentity = identity ?? throw new ArgumentNullException(nameof(identity));
        return this;
    }

    public IIndexBuilder WithColumns(Func<IndexColumnCollectionBuilder, IndexColumnCollectionBuilder> columns)
    {
        Columns = columns(Columns);
        return this;
    }

    public Index Build()
    {
        if (Identity is null)
            throw new InvalidOperationException("An index identity must be provided.");
        if (TableIdentity is null)
            throw new InvalidOperationException("A table identity must be provided.");
        var columns = Columns.Select(c => c.Build()).ToArray();

        if (columns.GroupBy(c => c.Name).Count() != columns.Length)
            throw new InvalidOperationException("Column names must be unique.");

        return new Index(Identity, TableIdentity, columns);
    }
}
