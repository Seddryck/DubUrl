using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public abstract class DatabaseAttribute : Attribute
{
    public virtual string DatabaseName { get; protected set; } = string.Empty;
    public virtual string[] Aliases { get; protected set; } = [];
    public virtual Type DialectType { get; protected set; } = typeof(AnsiDialect);
    public int ListingPriority { get; protected set; } = 0;

    public DatabaseAttribute(string databaseName, string[] aliases, Type dialectType, DatabaseCategory listingPriority)
    {
        DatabaseName = databaseName;
        Aliases = aliases;
        DialectType = dialectType;
        ListingPriority = (int)listingPriority;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DatabaseAttribute<D> : DatabaseAttribute where D : IDialect
{
    public DatabaseAttribute(string databaseName, string[] aliases, DatabaseCategory listingPriority)
        : base(
              databaseName
              , aliases
              , typeof(D)
              , listingPriority
        )
    { }
}

public enum DatabaseCategory
{
    TopPlayer = 0,
    LargePlayer = 1,
    InMemory = 2,
    Warehouse = 3,
    Analytics = 4,
    DistributedQueryEngine = 5,
    FileBased = 6,
    TimeSeries = 7,
}
