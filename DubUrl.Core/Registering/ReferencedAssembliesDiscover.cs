using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering
{
    public class ReferencedAssembliesDiscover : IProviderFactoriesDiscover
    {
        private Assembly? EntryAssembly { get; } = null;
        private List<Func<AssemblyName, bool>> Exclusions { get; } = new();

        public ReferencedAssembliesDiscover()
            : this(Assembly.GetEntryAssembly()) { }

        public ReferencedAssembliesDiscover(Assembly? assembly)
        {
            EntryAssembly = assembly;
            Exclusions.Add((AssemblyName reference)
                => !reference.FullName.Contains("System.") || reference.FullName.Contains("System.Data")
            );
            Exclusions.Add((AssemblyName reference)
                => !reference.FullName.Contains("Microsoft.") || reference.FullName.Contains("Microsoft.Data")
            );
        }

        public virtual IEnumerable<Type> Execute()
        {
            if (EntryAssembly == null)
                yield break;

            var list = new List<string>();
            var stack = new Stack<Assembly>();

            stack.Push(EntryAssembly);
            do
            {
                var asm = stack.Pop();
                Debug.WriteLine($"Analyzing assembly {asm.FullName}");

                //Check if assembly contains a DbProviderFactory
                var types = asm.ExportedTypes;
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

                //Check referenced assemblies of this assembly
                foreach (var reference in asm.GetReferencedAssemblies())
                    if (!list.Contains(reference.FullName) 
                        && (Exclusions.All(x => x.Invoke(reference)))
                    )
                    {
                        try { 
                            stack.Push(Assembly.Load(reference));
                            list.Add(reference.FullName);
                        }
                        catch { }
                    }
            }
            while (stack.Count > 0);
        }
    }
}
