using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class Index
{
    public string Name { get; }
    public string TableName { get; }
    public OrderedImmutableDictionary<string, IndexColumn> Columns { get; }

    public Index(string name, string tableName, IndexColumn[] columns)
    {
        Name = name;
        TableName = tableName;
        Columns = OrderedImmutableDictionary<string, IndexColumn>.From(
                    columns.Select(c => new KeyValuePair<string, IndexColumn>(c.Name, c)));
    }
}
