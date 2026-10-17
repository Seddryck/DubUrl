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
    private PrimaryKeyConstraint(Column[] columns, string? name, bool allowEmpty)
        : base(name)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (!allowEmpty && columns.Length == 0)
            throw new ArgumentException("A table primary key must contain at least one column.", nameof(columns));
        if (columns.Any(column => column is null))
            throw new ArgumentException("Primary key columns cannot contain null values.", nameof(columns));

        var duplicateNames = columns.GroupBy(column => column.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
            throw new ArgumentException(
                $"Primary key column names must be unique: {string.Join(", ", duplicateNames)}.",
                nameof(columns));

        Columns = OrderedImmutableDictionary<string, Column>.From(
                    columns.Select(c => new KeyValuePair<string, Column>(c.Name, c)));
    }

    public PrimaryKeyConstraint(Column[] columns)
        : this(columns, null, false) { }

    public PrimaryKeyConstraint(Column[] columns, string? name)
        : this(columns, name, false) { }

    public PrimaryKeyConstraint(Column column, string? name = null)
        : this([column], name, false) { }

    public PrimaryKeyConstraint(string? name = null)
        : this([], name, true) { }
}
