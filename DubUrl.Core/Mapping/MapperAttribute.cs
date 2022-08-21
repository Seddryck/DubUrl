using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class MapperAttribute<T> : MapperAttribute where T : IDatabase
    {
        public MapperAttribute(string providerInvariantName)
            : base(
                  typeof(T)
                  , providerInvariantName
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class MapperAttribute : BaseMapperAttribute
    {
        public MapperAttribute(Type database, string providerInvariantName)
            : base(database, providerInvariantName)
        { }
    }

}
