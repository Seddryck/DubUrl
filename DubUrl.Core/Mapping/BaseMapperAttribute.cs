using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class BaseMapperAttribute : Attribute
    {
        public virtual string DatabaseName { get; protected set; } = string.Empty;
        public virtual string[] Aliases { get; protected set; } = Array.Empty<string>();
        public virtual string ProviderInvariantName { get; protected set; } = string.Empty;
        public int ListingPriority { get; protected set; } = 0;
    }
}
