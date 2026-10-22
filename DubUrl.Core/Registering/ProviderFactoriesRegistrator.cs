using DubUrl.Extensions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering;

public class ProviderFactoriesRegistrator
{
    private IProviderFactoriesDiscoverer Discover { get; }

    public ProviderFactoriesRegistrator()
        : this(Assembly.GetCallingAssembly()) { }

    public ProviderFactoriesRegistrator(Assembly asm)
        : this(new BinFolderDiscoverer(new[] { asm })) { }

    public ProviderFactoriesRegistrator(IProviderFactoriesDiscoverer discover)
        => Discover = discover;

    internal static IEnumerable<Type> FindFactoryTypes(Assembly assembly)
        => assembly.GetExportedTypes().Where(type => type.IsClass && type.IsVisible && !type.IsAbstract
            && typeof(DbProviderFactory).IsAssignableFrom(type));

    internal static DbProviderFactory GetFactoryInstance(Type type)
        => (type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)) as DbProviderFactory
            ?? throw new InvalidOperationException($"Factory '{type.FullName}' does not expose a public static DbProviderFactory Instance.");

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
                var instance = GetFactoryInstance(type);
                DbProviderFactories.RegisterFactory(providerInvariantName, instance);
            }
        }
    }
}
