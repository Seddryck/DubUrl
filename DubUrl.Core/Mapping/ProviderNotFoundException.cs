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
            : base($"The ADO.Net provider corresponding to the invariant name '{providerName}' is not registered. " +
                  $"The list of valid providers is " +
                  $"{(validProviderNames.Length == 0 ? "empty" : "'" + string.Join("', '", validProviderNames) + "'")}. " +
                  $"{(validProviderNames.Length==0 ? "Did you forget to register the ADO.Net providers in the DbProviderFactories?" : "Did you forget to register this specific ADO.Net providers in the DbProviderFactories or did you use another invariant name?")}.")
        { }
        
    }
}
