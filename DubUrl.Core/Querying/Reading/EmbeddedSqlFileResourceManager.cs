using DubUrl.Querying.Templating;
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
        protected Assembly ResouceAssembly { get; }
        public virtual string[] ResourceNames { get; }

        public EmbeddedSqlFileResourceManager(Assembly assembly)
        {
            ResouceAssembly = assembly;
            ResourceNames = assembly.GetManifestResourceNames();
        }

        public string ReadResource(string resourceName)
        {
            using var stream =
                ResouceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new ArgumentException();
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public ParameterInfo[] ReadParameters(string resourceName) => Array.Empty<ParameterInfo>();

        public bool Any(string id, string[] dialects, string? connectivity)
            => ListResourceMathing(id, dialects, connectivity).Any();

        public string BestMatch(string id, string[] dialects, string? connectivity)
            => ListResourceMathing(id, dialects, connectivity).OrderBy(x => x.Score).Select(x => x.Path).First();
        
        protected record struct ResourceMatch(string Path, byte Score) { }
        protected virtual IEnumerable<ResourceMatch> ListResourceMathing(string id, string[] dialects, string? connectivity, string extension = "sql")
            => dialects
                    .Select(dialect => new ResourceMatch($"{id}.{connectivity}.{dialect}.{extension}", 0)).Where(x => !string.IsNullOrEmpty(connectivity))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{connectivity}.{extension}", 1)).Where(x => !string.IsNullOrEmpty(connectivity)))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{dialect}.{extension}", 2)))
                    .Append(new ResourceMatch($"{id}.{extension}", 3))
                    .Where(x => ResourceNames.Any(y => x.Path.Equals(y, StringComparison.InvariantCultureIgnoreCase)));

    }
}
