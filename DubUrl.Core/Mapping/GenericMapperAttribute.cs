using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class GenericMapperAttribute<T> : GenericMapperAttribute where T : IGenericConnectivity
    {
        public GenericMapperAttribute(string providerInvariantName)
            : base(
                  typeof(T)
                  , providerInvariantName
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class GenericMapperAttribute : BaseMapperAttribute
    {
        public GenericMapperAttribute(Type database, string providerInvariantName)
            : base(database, providerInvariantName)
        { }
    }
}
