using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    public class EmbeddedSqlTemplateResourceManager : EmbeddedSqlFileResourceManager, IResourceTemplateManager
    {
        public EmbeddedSqlTemplateResourceManager(Assembly assembly)
            : base(assembly) { }

        protected override IEnumerable<ResourceMatch> ListResourceMathing(string id, string[] dialects, string? connectivity, string extension = "sql")
            => base.ListResourceMathing(id, dialects, connectivity, "sql.st");


        public IDictionary<string, string> ListResources(string directory, string[] dialects, string? connectivity, string extension = "sql.st")
        {
            var dico = new Dictionary<string, string>();

            var resources = ResourceNames
                .Where(x => x.StartsWith(directory))
                .Where(x => x.EndsWith(".sql.st"))
                .Select(x => x.Split(".")[..^2].Last())
                .Distinct();

            foreach (var resource in resources)
            {
                var path = dialects
                     .Select(dialect => new ResourceMatch($"{directory}.{connectivity}.{dialect}.{resource}.{extension}", 0)).Where(x => !string.IsNullOrEmpty(connectivity))
                     .Union(dialects.Select(dialect => new ResourceMatch($"{directory}.{connectivity}.{resource}.{extension}", 1)).Where(x => !string.IsNullOrEmpty(connectivity)))
                     .Union(dialects.Select(dialect => new ResourceMatch($"{directory}.{dialect}.{resource}.{extension}", 2)))
                     .Append(new ResourceMatch($"{directory}.{resource}.{extension}", 3))
                     .Where(x => ResourceNames.Any(y => x.Path.Equals(y, StringComparison.InvariantCultureIgnoreCase)))
                     .OrderBy(x => x.Score).Select(x => x.Path).First();

                dico.Add(resource, path);
            }
            return dico;
        }
    }
}
