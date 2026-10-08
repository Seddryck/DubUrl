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
    public DatabaseObjectName Identity { get; }
    public OrderedImmutableDictionary<string, Column> Columns { get; }
    public ConstraintCollection Constraints { get; }
    public string? Description { get; }

    public Table(string name, Column[] columns, Constraint[]? constraints = null, string? description = null)
        : this(new DatabaseObjectName(name), columns, constraints, description)
    { }

    public Table(DatabaseObjectName identity, Column[] columns, Constraint[]? constraints = null, string? description = null)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Name = identity.Name;
        Columns = OrderedImmutableDictionary<string, Column>.From(
                    columns.Select(c => new KeyValuePair<string, Column>(c.Name, c)));
        Constraints = new(constraints ?? []);
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
    }
}
