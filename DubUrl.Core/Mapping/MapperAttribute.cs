using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class MapperAttribute : BaseMapperAttribute
    {
        public MapperAttribute(string databaseName, string[] aliases, string providerInvariantName, int listingPriority = 5)
            => (DatabaseName, Aliases, ProviderInvariantName, ListingPriority) 
                = (databaseName, aliases, providerInvariantName, listingPriority);       
    }

    
}
