using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

/// <summary>
/// Adds a new table to the collection.
/// </summary>
/// <param name="tableBuilder">A function that configures and returns a table builder.</param>
/// <returns>This instance for method chaining.</returns>
public class TableCollectionBuilder : IEnumerable<ITableBuilder>
{
    private List<ITableBuilder> Tables { get; } = [];

    public TableCollectionBuilder Add(Func<TableBuilder, ITableBuilder> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Tables.Add(table(new()));
        return this;
    }

    public void Add(ITableBuilder item) => Tables.Add(item);
    public void Clear() => Tables.Clear();
    public bool Contains(ITableBuilder item) => Tables.Contains(item);
    public void CopyTo(ITableBuilder[] array, int arrayIndex) => Tables.CopyTo(array, arrayIndex);
    public bool Remove(ITableBuilder item) => Tables.Remove(item);

    public IEnumerator<ITableBuilder> GetEnumerator()
        => Tables.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
