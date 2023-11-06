using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WrapperMapperAttribute<T, P> : WrapperMapperAttribute
        where T : IWrapperConnectivity
        where P : IParametrizer
    {
        public WrapperMapperAttribute(string providerInvariantName)
            : base(
                  typeof(T)
                  , typeof(P)
                  , providerInvariantName
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class WrapperMapperAttribute : BaseMapperAttribute
    {
        public WrapperMapperAttribute(Type database, Type parametrizer, string providerInvariantName)
            : base(database, parametrizer, providerInvariantName)
        { }
    }
}
