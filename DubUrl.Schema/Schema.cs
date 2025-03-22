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
        Tables = OrderedImmutableDictionary<string, Table>.From(
                    tables.Select(t => new KeyValuePair<string, Table>(t.Name, t)));
    }
}
