using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Schema
{
    public OrderedImmutableDictionary<string, Table> Tables { get; }
    public Schema(Table[] tables)
    {
        // Check for duplicate table names
        var duplicates = tables.GroupBy(t => t.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        if (duplicates.Length > 0)
            throw new ArgumentException($"Duplicate table names found: {string.Join(", ", duplicates)}", nameof(tables));

        Tables = OrderedImmutableDictionary<string, Table>.From(
            tables.Select(t => new KeyValuePair<string, Table>(t.Name, t)));
    }
}
