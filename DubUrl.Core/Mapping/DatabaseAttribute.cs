using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class DatabaseAttribute : Attribute
    {
        public virtual string DatabaseName { get; protected set; } = string.Empty;
        public virtual string[] Aliases { get; protected set; } = Array.Empty<string>();
        public virtual Type DialectType { get; protected set; } = typeof(AnsiDialect);
        public int ListingPriority { get; protected set; } = 0;

        public DatabaseAttribute(string databaseName, string[] aliases, Type dialectType, int listingPriority)
        {
            DatabaseName = databaseName;
            Aliases = aliases;
            DialectType = dialectType;
            ListingPriority = listingPriority;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class DatabaseAttribute<D> : DatabaseAttribute where D : IDialect
    {
        public DatabaseAttribute(string databaseName, string[] aliases, int listingPriority = 5)
            : base(
                  databaseName
                  , aliases
                  , typeof(D)
                  , listingPriority
            )
        { }
    }
}
