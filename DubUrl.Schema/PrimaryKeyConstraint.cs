using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class PrimaryKeyConstraint : Constraint
{
    public string? Name { get; }
    public OrderedImmutableDictionary<string, Column> Columns { get; }
    private PrimaryKeyConstraint(Column[] columns, string? name)
    {
        Name = name;
        Columns = OrderedImmutableDictionary<string, Column>.From(
                    columns.Select(c => new KeyValuePair<string, Column>(c.Name, c)));
    }

    public PrimaryKeyConstraint(Column[] columns)
        : this(columns, null) { }

    public PrimaryKeyConstraint(string name, Column[] columns)
        : this(columns, name) { }
}
