using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class Table
{
    public string Name { get; }
    public OrderedImmutableDictionary<string, Column> Columns { get; }
    public ConstraintCollection Constraints { get; }

    public Table(string name, Column[] columns, Constraint[]? constraints = null)
    {
        Name = name;
        Columns = OrderedImmutableDictionary<string, Column>.From(
                    columns.Select(c => new KeyValuePair<string, Column>(c.Name, c)));
        Constraints = new(constraints ?? []);
    }
}
