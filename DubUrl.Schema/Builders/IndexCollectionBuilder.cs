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
/// <param name="indexBuilder">A function that configures and returns an index builder.</param>
/// <returns>This instance for method chaining.</returns>
public class IndexCollectionBuilder : IEnumerable<IIndexBuilder>
{
    private List<IIndexBuilder> Indexs { get; } = [];

    public IndexCollectionBuilder Add(Func<IndexBuilder, IIndexBuilder> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Indexs.Add(table(new()));
        return this;
    }

    public void Add(IIndexBuilder item) => Indexs.Add(item);
    public void Clear() => Indexs.Clear();
    public bool Contains(IIndexBuilder item) => Indexs.Contains(item);
    public void CopyTo(IIndexBuilder[] array, int arrayIndex) => Indexs.CopyTo(array, arrayIndex);
    public bool Remove(IIndexBuilder item) => Indexs.Remove(item);

    public IEnumerator<IIndexBuilder> GetEnumerator()
        => Indexs.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
