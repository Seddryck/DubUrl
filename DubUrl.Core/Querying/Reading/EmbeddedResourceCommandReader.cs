using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class EmbeddedResourceCommandReader : ICommandReader
    {
        private Assembly ResouceAssembly { get; }

        public EmbeddedResourceCommandReader(Assembly assembly)
            => ResouceAssembly = assembly;

        
        public string[] GetAllResourceNames()
            => ResouceAssembly.GetManifestResourceNames();

        

        public string ReadCommandText(string resourceName)
        {
            using var stream =
                ResouceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new ArgumentException();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public ParameterInfo[] ReadParameters(string resourceName) => Array.Empty<ParameterInfo>();
    }
}
