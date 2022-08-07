using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    sealed public class AlternativeMapperAttribute<T> : AlternativeMapperAttribute where T : BaseMapper
    {
        public override string DatabaseName
        => typeof(T).GetCustomAttribute<MapperAttribute>()?.DatabaseName
            ?? throw new Exception();
        public override string[] Aliases
        => typeof(T).GetCustomAttribute<MapperAttribute>()?.Aliases
                ?? throw new Exception();
        public AlternativeMapperAttribute(string providerInvariantName) : base()
            => (ProviderInvariantName) = (providerInvariantName);
    }

    public abstract class AlternativeMapperAttribute : BaseMapperAttribute { }
}
