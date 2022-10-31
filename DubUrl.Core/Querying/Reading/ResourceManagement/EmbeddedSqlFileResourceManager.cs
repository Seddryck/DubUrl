using DubUrl.Querying.Reading.ResourceMatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceManagement
{
    public class EmbeddedSqlFileResourceManager : IResourceManager
    {
        private Assembly ResourceAssembly { get; }

        public EmbeddedSqlFileResourceManager(Assembly assembly)
            => ResourceAssembly = assembly;

        public string ReadCommandText(string resourceName)
        {
            using var stream =
                ResourceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new ArgumentException();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public virtual string[] ListResources()
            => ResourceAssembly.GetManifestResourceNames();
    }
}
