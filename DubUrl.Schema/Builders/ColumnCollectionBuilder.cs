using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;
public class ColumnCollectionBuilder : IEnumerable<IColumnBuilder>
{
    private List<IColumnBuilder> Columns { get; } = [];

    public ColumnCollectionBuilder Add(Func<ColumnBuilder, IColumnBuilder> column)
    {
        Columns.Add(column(new()));
        return this;
    }

    public IEnumerator<IColumnBuilder> GetEnumerator()
        => Columns.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
