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

        var tableConstraints = constraints ?? [];
        var primaryKeys = tableConstraints.OfType<PrimaryKeyConstraint>().ToArray();
        if (primaryKeys.Length > 1)
            throw new ArgumentException($"Table '{Name}' cannot contain more than one primary key.", nameof(constraints));

        if (primaryKeys.Length == 1)
        {
            var primaryKey = primaryKeys[0];
            var unknownColumns = primaryKey.Columns.Keys.Where(column => !Columns.ContainsKey(column)).ToArray();
            if (unknownColumns.Length > 0)
                throw new ArgumentException(
                    $"Primary key references unknown column(s) {string.Join(", ", unknownColumns)} in table '{Name}'.",
                    nameof(constraints));

            var normalizedPrimaryKey = new PrimaryKeyConstraint(
                primaryKey.Columns.Keys.Select(column => Columns[column]).ToArray(),
                primaryKey.Name);
            tableConstraints = tableConstraints
                .Select(constraint => ReferenceEquals(constraint, primaryKey) ? normalizedPrimaryKey : constraint)
                .ToArray();
        }

        foreach (var unique in tableConstraints.OfType<UniquenessConstraint>())
        {
            if (unique.Columns.Count == 0)
                throw new ArgumentException($"Table '{Name}' UNIQUE constraint requires explicit columns.", nameof(constraints));
            if (unique.Columns.Any(column => !Columns.ContainsKey(column.Name)))
                throw new ArgumentException($"Table '{Name}' UNIQUE constraint references an unknown column.", nameof(constraints));
        }
        Constraints = new(tableConstraints);
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
    }
}
