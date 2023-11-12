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
        protected string FallbackPath { get; } = "Common";
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
                    ?? throw new FileNotFoundException(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public ParameterInfo[] ReadParameters(string resourceName) => Array.Empty<ParameterInfo>();

        public virtual bool Any(string id, IResourceMatchingOption option)
        {
            var matchingOption = (option as DirectCommandMatchingOption) ?? throw new InvalidCastException(nameof(option));
            return ListResourceMathing(id, matchingOption.Dialects, matchingOption.Connectivity).Any();
        }

        public virtual string BestMatch(string id, IResourceMatchingOption option)
        {
            var matchingOption = (option as DirectCommandMatchingOption) ?? throw new InvalidCastException(nameof(option));
            return ListResourceMathing(id, matchingOption.Dialects, matchingOption.Connectivity).OrderBy(x => x.Score).Select(x => x.Path).First();
        }
        
        protected record struct ResourceMatch(string Path, byte Score) { }
        protected virtual IEnumerable<ResourceMatch> ListResourceMathing(string id, string[] dialects, string? connectivity, string extension = "sql")
            => dialects
                    .Select(dialect => new ResourceMatch($"{id}.{connectivity}.{dialect}.{extension}", 0)).Where(x => !string.IsNullOrEmpty(connectivity))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{connectivity}.{FallbackPath}.{extension}", 10)).Where(x => !string.IsNullOrEmpty(connectivity)))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{connectivity}.{extension}", 20)).Where(x => !string.IsNullOrEmpty(connectivity)))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{dialect}.{extension}", 30)))
                    .Union(dialects.Select(dialect => new ResourceMatch($"{id}.{FallbackPath}.{extension}", 40)))
                    .Append(new ResourceMatch($"{id}.{extension}", 50))
                    .Where(x => ResourceNames.Any(y => x.Path.Equals(y, StringComparison.InvariantCultureIgnoreCase)));
    }
}
