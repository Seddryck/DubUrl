using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceMatching
{
    public class ResourceMatcherFactory : IResourceMatcherFactory
    {
        private readonly Dictionary<Type, Func<string[], IResourceMatcher>> dico = new();

        public ResourceMatcherFactory()
        {
            Initialize();
        }

        public void Initialize()
        { 
            dico.Add(typeof(OdbcConnectivity), (string[] dialects) => new OdbcDriverResourceMatcher(dialects));
            dico.Add(typeof(NativeConnectivity), (string[] dialects) => new AdoNetResourceMatcher(dialects));
        }

        public void Initialize(IResourceMatcherFactoryInitializer initializer)
            => dico.Add(initializer.Connectivity.GetType(), initializer.Execute());

        public ResourceMatcherFactory Add(IConnectivity connectivity, Func<string[], IResourceMatcher> func)
        {
            dico.Add(connectivity.GetType(), func);
            return this;
        }

        public IResourceMatcher Instantiate(IConnectivity connectivity, string[] dialects)
            => dico[connectivity.GetType()].Invoke(dialects);
    }
}