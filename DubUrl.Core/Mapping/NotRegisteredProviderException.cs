using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;
public class NotRegisteredProviderException : DubUrlException
{
    public NotRegisteredProviderException(string providerInvariantName)
        : base($"No provider registered with the name '{providerInvariantName}'.") { }
}
