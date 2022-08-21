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
        public virtual Type Database { get; protected set; } = typeof(object);
        public virtual string ProviderInvariantName { get; protected set; } = string.Empty;

        protected BaseMapperAttribute(Type database, string providerInvariantName)
            => (Database, ProviderInvariantName) = (database, providerInvariantName);
    }
}
