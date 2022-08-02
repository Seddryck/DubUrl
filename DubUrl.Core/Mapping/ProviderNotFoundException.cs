using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class ProviderNotFoundException : DubUrlException
    {
        public ProviderNotFoundException(string providerName, string[] validProviderNames)
            : base($"The provider '{providerName}' is not available. The list of valid provider is '{string.Join("', '", validProviderNames)}'.")
        { }
        
    }
}
