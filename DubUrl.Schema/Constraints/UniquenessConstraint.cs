using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class UniquenessConstraint : Constraint
{
    /// <summary>Empty for a column constraint; explicit column set for a table constraint.</summary>
    public IReadOnlyList<Column> Columns { get; } = [];

    public UniquenessConstraint(Column[] columns, string? name = null) : base(name)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0 || columns.Any(column => column is null)
            || columns.Select(column => column.Name).Distinct(StringComparer.Ordinal).Count() != columns.Length)
            throw new ArgumentException("A table UNIQUE constraint requires distinct, non-null columns.", nameof(columns));
        Columns = Array.AsReadOnly(columns.ToArray());
    }

    public UniquenessConstraint(string? name = null)
        : base(name)
    { }
}
