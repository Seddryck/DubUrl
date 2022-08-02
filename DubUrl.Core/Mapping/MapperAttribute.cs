using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class MapperAttribute : Attribute
    {
        public string DatabaseName { get; }
        public string[] Aliases { get; }
        public string ProviderInvariantName { get; }

        public int ListingPriority { get; }

        public MapperAttribute(string databaseName, string[] aliases, string providerInvariantName, int listingPriority = 5)
            => (DatabaseName, Aliases, ProviderInvariantName, ListingPriority) 
                = (databaseName, aliases, providerInvariantName, listingPriority);       
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    sealed public class AlternativeMapperAttribute : Attribute
    {
        public Type AlternativeTo { get; }

        public AlternativeMapperAttribute(Type alternativeTo)
            => (AlternativeTo) = (alternativeTo);
    }
}
