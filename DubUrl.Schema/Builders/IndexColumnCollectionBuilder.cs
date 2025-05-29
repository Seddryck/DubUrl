using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;
public class IndexColumnCollectionBuilder : IEnumerable<IIndexColumnBuilder>
{
    private List<IIndexColumnBuilder> Columns { get; } = [];

    public IndexColumnCollectionBuilder Add(Func<IndexColumnBuilder, IIndexColumnBuilder> column)
    {
        Columns.Add(column(new()));
        return this;
    }

    public IEnumerator<IIndexColumnBuilder> GetEnumerator()
        => Columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
