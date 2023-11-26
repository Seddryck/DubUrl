using DubUrl.Extensions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering
{
    public class ProviderFactoriesRegistrator
    {
        private IProviderFactoriesDiscoverer Discover { get; }

        public ProviderFactoriesRegistrator()
            : this(Assembly.GetCallingAssembly()) { }

        public ProviderFactoriesRegistrator(Assembly asm)
            : this(new BinFolderDiscoverer(new[] { asm })) { }

        public ProviderFactoriesRegistrator(IProviderFactoriesDiscoverer discover)
            => Discover = discover;

        public void Register()
        {
            var types = Discover.Execute();

            foreach (var type in types)
            {
                string providerInvariantName = 
                    type.GetCustomAttribute<ProviderInvariantNameAttribute>()?.Value
                    ?? type.Assembly.GetName().Name
                    ?? throw new ArgumentNullException();
                
                if (!DbProviderFactories.GetProviderInvariantNames().Contains(providerInvariantName))
                {
                    var instance = (DbProviderFactory)(type.GetField("Instance")?.GetValue(null) ?? throw new ArgumentNullException());
                    DbProviderFactories.RegisterFactory(providerInvariantName, instance);
                }
            }
        }
    }
}
