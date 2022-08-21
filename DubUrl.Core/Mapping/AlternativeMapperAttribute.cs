using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class AlternativeMapperAttribute<T> : AlternativeMapperAttribute where T : IDatabase
    {
        public AlternativeMapperAttribute(string providerInvariantName)
            : base(
                  typeof(T)
                  , providerInvariantName
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AlternativeMapperAttribute : BaseMapperAttribute
    {
        public AlternativeMapperAttribute(Type database, string providerInvariantName)
            : base(database, providerInvariantName)
        { }
    }
}
