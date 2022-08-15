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
        private IProviderFactoriesDiscover Discover { get; }

        public ProviderFactoriesRegistrator()
            : this(Assembly.GetCallingAssembly()) { }

        public ProviderFactoriesRegistrator(Assembly asm)
            : this(new BinFolderDiscover(asm)) { }

        public ProviderFactoriesRegistrator(IProviderFactoriesDiscover discover)
            => Discover = discover;

        public void Register()
        {
            var types = Discover.Execute();

            foreach (var type in types)
            {
                var assemblyName = type.Assembly.GetName().Name ?? throw new ArgumentNullException();
                if (!DbProviderFactories.GetProviderInvariantNames().Contains(assemblyName))
                {
                    var instance = (DbProviderFactory)(type.GetField("Instance")?.GetValue(null) ?? throw new ArgumentNullException());
                    DbProviderFactories.RegisterFactory(type.Assembly.GetName().Name ?? throw new ArgumentNullException(), instance);
                }
            }
        }
    }
}
