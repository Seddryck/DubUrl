using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class BaseMapperAttribute : Attribute
    {
        public virtual Type Connectivity { get; protected set; } = typeof(object);
        public virtual Type Parametrizer { get; protected set; } = typeof(object);
        public virtual string ProviderInvariantName { get; protected set; } = string.Empty;

        protected BaseMapperAttribute(Type database, Type parametrizer, string providerInvariantName)
            => (Connectivity, Parametrizer, ProviderInvariantName) = (database, parametrizer, providerInvariantName);
    }
}
