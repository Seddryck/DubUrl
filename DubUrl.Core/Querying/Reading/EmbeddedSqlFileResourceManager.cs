using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class EmbeddedSqlFileResourceManager : IResourceManager
    {
        private Assembly ResouceAssembly { get; }
        public virtual string[] ResourceNames { get; }

        public EmbeddedSqlFileResourceManager(Assembly assembly)
        {
            ResouceAssembly = assembly;
            ResourceNames = assembly.GetManifestResourceNames();
        }

        public string ReadCommandText(string resourceName)
        {
            using var stream =
                ResouceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new ArgumentException();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public ParameterInfo[] ReadParameters(string resourceName) => Array.Empty<ParameterInfo>();

        public bool Any(string id, string[] dialects)
            => ListResourceMathing(id, dialects).Any();

        public string BestMatch(string id, string[] dialects)
            => ListResourceMathing(id, dialects).OrderByDescending(x => x.Length).First();

        protected virtual IEnumerable<string> ListResourceMathing(string id, string[] dialects)
            => dialects.Select(dialect => $"{id}.{dialect}.sql").Append($"{id}.sql")
                .Where(x => ResourceNames.Any(y => x.Equals(y, StringComparison.InvariantCultureIgnoreCase)));
    }
}
