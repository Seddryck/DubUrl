using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DriverAttribute : Attribute
    {
        public string DatabaseName { get; }
        public string[] Aliases { get; }
        public string NamePattern { get; }
        public Type[] Options { get; } 
        public int ListingPriority { get; }

        public DriverAttribute(string databaseName, string[] aliases, string namePattern, Type[]? options = null, int listingPriority = 5)
            => (DatabaseName, Aliases, NamePattern, Options, ListingPriority) 
                = (databaseName, aliases, namePattern, options ?? Array.Empty<Type>(), listingPriority);       
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class DriverAttribute<R> : DriverAttribute where R : IRegexDriver
    {
        public DriverAttribute(string databaseName, string[] aliases, Type[]? options = null, int listingPriority = 5)
            : base(
                  databaseName
                  , aliases
                  , Activator.CreateInstance<R>().ToString()
                  , options ?? Array.Empty<Type>()
                  , listingPriority
            )
        { }
    }
}
