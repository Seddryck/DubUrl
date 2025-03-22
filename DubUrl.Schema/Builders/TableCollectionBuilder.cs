using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;
public class TableCollectionBuilder : IEnumerable<ITableBuilder>
{
    private List<ITableBuilder> Tables { get; } = [];

    public TableCollectionBuilder Add(Func<TableBuilder, ITableBuilder> column)
    {
        Tables.Add(column(new()));
        return this;
    }

    public IEnumerator<ITableBuilder> GetEnumerator()
        => Tables.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
