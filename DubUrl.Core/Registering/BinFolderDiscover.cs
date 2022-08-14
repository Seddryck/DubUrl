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

namespace DubUrl.Registering
{
    public class BinFolderDiscover : IProviderFactoriesDiscover
    {
        private Assembly? Assembly { get; } = null;
        private MapperIntrospector MapperIntrospector { get; } = new ();

        public BinFolderDiscover()
            : this(Assembly.GetEntryAssembly()) { }

        internal BinFolderDiscover(MapperIntrospector mapperIntrospector)
            : this(Assembly.GetEntryAssembly(), mapperIntrospector) { }

        public BinFolderDiscover(Assembly? assembly)
            : this(assembly, new MapperIntrospector()) { }

        internal BinFolderDiscover(Assembly? assembly, MapperIntrospector mapperIntrospector)
            => (Assembly, MapperIntrospector) = (assembly, mapperIntrospector);

        public virtual IEnumerable<Type> Execute()
        {
            if (Assembly == null || string.IsNullOrEmpty(Assembly.Location))
                yield break;

            var rootPath = Path.GetDirectoryName(Assembly.Location);
            if (!Directory.Exists(rootPath))
                yield break;

            var listCandidates = MapperIntrospector.Locate().Select(x => x.ProviderInvariantName);
            var stack = new Stack<string>(Directory.GetFiles(rootPath));

            while (stack.Count > 0)
            {
                var asmPath = stack.Pop();
                var asmName = Path.GetFileNameWithoutExtension(asmPath) ?? throw new ArgumentNullException();
                Debug.WriteLine($"Analyzing assembly {asmName}");

                if (!listCandidates.Contains(asmName))
                    continue;
                Debug.WriteLine($"Mapper found for assembly {asmName}");

                var asm = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == asmName)
                    ?? AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(asmPath));

                //Check if assembly contains a DbProviderFactory
                var types = asm.DefinedTypes;
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
}
