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

    public Index(string name, string tableName, IndexColumn[] columns)
        : this(new DatabaseObjectName(name), new DatabaseObjectName(tableName), columns)
    { }

    public Index(DatabaseObjectName identity, DatabaseObjectName tableIdentity, IndexColumn[] columns)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        TableIdentity = tableIdentity ?? throw new ArgumentNullException(nameof(tableIdentity));
        Name = identity.Name;
        TableName = tableIdentity.Name;
        Columns = OrderedImmutableDictionary<string, IndexColumn>.From(
                    columns.Select(c => new KeyValuePair<string, IndexColumn>(c.Name, c)));
    }
}
