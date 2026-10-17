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
    public DatabaseObjectName Identity { get; }
    public DatabaseObjectName TableIdentity { get; }
    public OrderedImmutableDictionary<string, IndexColumn> Columns { get; }
    public bool IsUnique { get; }

    public Index(string name, string tableName, IndexColumn[] columns)
        : this(new DatabaseObjectName(name), new DatabaseObjectName(tableName), columns, false)
    { }

    public Index(string name, string tableName, IndexColumn[] columns, bool isUnique)
        : this(new DatabaseObjectName(name), new DatabaseObjectName(tableName), columns, isUnique)
    { }

    public Index(DatabaseObjectName identity, DatabaseObjectName tableIdentity, IndexColumn[] columns)
        : this(identity, tableIdentity, columns, false)
    { }

    public Index(DatabaseObjectName identity, DatabaseObjectName tableIdentity, IndexColumn[] columns, bool isUnique)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        TableIdentity = tableIdentity ?? throw new ArgumentNullException(nameof(tableIdentity));
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
            throw new ArgumentException("An index must contain at least one column.", nameof(columns));
        var duplicateNames = columns.GroupBy(column => column.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateNames.Length > 0)
            throw new ArgumentException(
                $"Index column names must be unique: {string.Join(", ", duplicateNames)}.",
                nameof(columns));

        Name = identity.Name;
        TableName = tableIdentity.Name;
        IsUnique = isUnique;
        Columns = OrderedImmutableDictionary<string, IndexColumn>.From(
                    columns.Select(c => new KeyValuePair<string, IndexColumn>(c.Name, c)));
    }
}
