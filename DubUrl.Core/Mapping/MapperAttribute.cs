using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class MapperAttribute : BaseMapperAttribute
    {
        public MapperAttribute(string databaseName, string[] aliases, string providerInvariantName, IDialect dialect, int listingPriority = 5)
        {
            (DatabaseName, Aliases, ProviderInvariantName, Dialect, ListingPriority)
                = (databaseName, aliases, providerInvariantName, dialect, listingPriority);
        }
    }


    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class MapperAttribute<T> : MapperAttribute where T : IDialect
    {
        public MapperAttribute(string databaseName, string[] aliases, string providerInvariantName, int listingPriority = 5)
            : base(
                  databaseName
                  , aliases
                  , providerInvariantName
                  , (IDialect)(Activator.CreateInstance(typeof(T), new object[] { aliases }) ?? throw new ArgumentException())
                  , listingPriority
            )
        { }
    }
}
