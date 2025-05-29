using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Schema
{
    public OrderedImmutableDictionary<string, Table> Tables { get; }
    public OrderedImmutableDictionary<string, Index> Indexes { get; }

    public Schema(Table[] tables)
        : this(tables, [])
    { }

    public Schema(Table[] tables, Index[] indexes)
    {
        // Check for duplicate table names
        var duplicates = tables.GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
            throw new ArgumentException($"Duplicate table names found: {string.Join(", ", duplicates)}", nameof(tables));

        Tables = OrderedImmutableDictionary<string, Table>.From(
            tables.Select(t => new KeyValuePair<string, Table>(t.Name, t)));

        // Check for duplicate index names
        var duplicatedIndexes = indexes.GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicatedIndexes.Length > 0)
            throw new ArgumentException($"Duplicate index names found: {string.Join(", ", duplicates)}", nameof(indexes));

        Indexes = OrderedImmutableDictionary<string, Index>.From(
            indexes.Select(t => new KeyValuePair<string, Index>(t.Name, t)));
    }
}
