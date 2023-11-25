using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ProviderInvariantNameAttribute : Attribute
    {
        public string Value { get; set; }
        public ProviderInvariantNameAttribute(string value)
            => Value = value;
    }
}
