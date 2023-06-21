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


        private record struct PartialResourceMatch(string End, byte Score) { }
        public IDictionary<string, string> ListResources(string directory, string[] dialects, string? connectivity, string extension = "sql.st")
        {
            var dico = new Dictionary<string, string>();

            var resources = ResourceNames.Where(x => x.StartsWith(directory) && x.EndsWith(".sql.st"));
            var ids = resources.Select(x => x.Split(".")[..^2].Last()).Distinct();

            foreach (var id in ids)
            {
                var candidates = resources.Where(x => x.Split(".")[..^2].Last() == id);

                var bestScore = dialects
                     .Select(dialect => new PartialResourceMatch($".{connectivity}.{dialect}.{id}.{extension}", 0)).Where(x => !string.IsNullOrEmpty(connectivity))
                     .Union(dialects.Select(dialect => new PartialResourceMatch($".{connectivity}.{id}.{extension}", 1)).Where(x => !string.IsNullOrEmpty(connectivity)))
                     .Union(dialects.Select(dialect => new PartialResourceMatch($".{dialect}.{id}.{extension}", 2)))
                     .Append(new PartialResourceMatch($".{id}.{extension}", 3))
                     .Where(x => candidates.Any(y => y.EndsWith(x.End, StringComparison.InvariantCultureIgnoreCase)))
                     .MinBy(x => x.Score);
                
                var path = candidates.Where(x => x.EndsWith(bestScore.End, StringComparison.InvariantCultureIgnoreCase))
                    .OrderBy(x => x.Split('.').Length)
                    .First();

                dico.Add(id, path);
            }
            return dico;
        }
    }
}
