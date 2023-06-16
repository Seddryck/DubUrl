using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class MapperAttribute<T, P> : MapperAttribute where T : IDatabase where P : IParametrizer
    {
        public MapperAttribute(string providerInvariantName)
            : base(
                  typeof(T)
                  , typeof(P)
                  , providerInvariantName
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class MapperAttribute : BaseMapperAttribute
    {
        public MapperAttribute(Type database, Type parametrizer, string providerInvariantName)
            : base(database, parametrizer, providerInvariantName)
        { }
    }

}
