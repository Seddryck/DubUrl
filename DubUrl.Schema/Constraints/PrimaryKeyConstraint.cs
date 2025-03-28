using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class PrimaryKeyConstraint : Constraint
{
    public OrderedImmutableDictionary<string, Column> Columns { get; }
    private PrimaryKeyConstraint(Column[] columns, string? name)
        : base(name)
    {
        Columns = OrderedImmutableDictionary<string, Column>.From(
                    columns.Select(c => new KeyValuePair<string, Column>(c.Name, c)));
    }

    public PrimaryKeyConstraint(Column[] columns)
        : this(columns, null) { }

    public PrimaryKeyConstraint(Column column, string? name = null)
        : this([column], name) { }

    public PrimaryKeyConstraint(string? name = null)
        : this([], name) { }
}
