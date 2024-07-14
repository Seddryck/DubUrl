using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering;

public class BinFolderDiscoverer : IProviderFactoriesDiscoverer
{
    private Assembly[] Assemblies { get; }
    private BaseMapperIntrospector[] MapperIntrospectors { get; }

    public BinFolderDiscoverer()
        : this(new[] { Assembly.GetEntryAssembly() ?? throw new InvalidOperationException() }) { }

    internal BinFolderDiscoverer(BaseMapperIntrospector[] mapperIntrospectors)
        : this([Assembly.GetEntryAssembly() ?? throw new InvalidOperationException()] , mapperIntrospectors) { }

    public BinFolderDiscoverer(Assembly[] assemblies)
        : this(assemblies,
            [
                new NativeMapperIntrospector([.. assemblies, .. new[] {typeof(NativeMapperIntrospector).Assembly }])
                , new WrapperMapperIntrospector([.. assemblies, .. new[] {typeof(WrapperMapperIntrospector).Assembly }]) 
            ]
        ) { }

    internal BinFolderDiscoverer(Assembly[] assemblies, BaseMapperIntrospector[] mapperIntrospectors)
        => (Assemblies, MapperIntrospectors) = (assemblies, mapperIntrospectors);

    public virtual IEnumerable<Type> Execute()
    {
        var files = Assemblies.Aggregate(
                Array.Empty<string>(), (seed, asm) =>
                [.. seed, .. new[] { asm?.Location ?? string.Empty }]
            )
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .Select(Path.GetDirectoryName)
            .Where(Directory.Exists)
            .Aggregate(
                Array.Empty<string>(), (seed, location) =>
                [.. seed, .. Directory.GetFiles(location!)]
            )
            .Where(x => Path.GetExtension(x) == ".dll");

        var stack = new Stack<string>(files);
        var listCandidates = MapperIntrospectors.Aggregate(
            Array.Empty<string>(), (seed, x) =>
                seed = [.. seed, .. x.Locate().Select(x => x.ProviderInvariantName)]
            ).Distinct();
        
        while (stack.Count > 0)
        {
            var asmPath = stack.Pop();
            var asmName = Path.GetFileNameWithoutExtension(asmPath) ?? throw new ArgumentNullException();
            Debug.WriteLine($"Analyzing assembly {asmName}");

            if (!listCandidates.Contains(asmName) && !asmName.StartsWith(typeof(BinFolderDiscoverer).Namespace!.Split('.')[0]))
                continue;
            Debug.WriteLine($"Mapper found for assembly {asmName}");

            var asm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == asmName)
                ?? AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(asmPath));

            //Check if assembly contains a DbProviderFactory
            var types = asm.GetExportedTypes();
            var providerFactories = types.Where(t =>
                    t.IsClass
                    && t.IsVisible
                    && !t.IsAbstract
                    && typeof(DbProviderFactory).IsAssignableFrom(t)
                );
            foreach (var providerFactory in providerFactories)
            {
                yield return providerFactory;
                Debug.WriteLine($"Provider factory found {providerFactory.Name}");
            }
        }
        
    }
}
