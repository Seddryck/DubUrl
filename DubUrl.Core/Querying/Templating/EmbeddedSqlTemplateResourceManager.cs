using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.IO;
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

            var resources = ResourceNames.Where(x => x.StartsWith(directory) && x.EndsWith(".sql.st"));
            var ids = resources.Select(x => x.Split(".")[..^2].Last()).Distinct();

            foreach (var id in ids)
            {
                var candidates = resources.Where(x => x.Split(".")[..^2].Last() == id);

                var scores = Enumerable.Empty<ResourceMatch>()
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{connectivity}.{dialect}.", StringComparison.InvariantCultureIgnoreCase))
                                                              && !string.IsNullOrEmpty(connectivity))
                                      .Select(candidate => new ResourceMatch(candidate, 0))
                           )
                     .Union(candidates.Where(candidate => candidate.Contains($".{connectivity}.", StringComparison.InvariantCultureIgnoreCase)
                                                              && !string.IsNullOrEmpty(connectivity))
                                      .Select(candidate => new ResourceMatch(candidate, 1))
                           )
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{dialect}.", StringComparison.InvariantCultureIgnoreCase)))
                                      .Select(candidate => new ResourceMatch(candidate, 2))
                           )
                     .Union(candidates.Select(candidate => new ResourceMatch(candidate, 3)))
                     .GroupBy(rm => rm.Path)
                     .Select(g => new ResourceMatch(g.Key, g.Where(x => x.Path == g.Key).Min(x => x.Score)));

                var bestScore = scores
                     .Where(x => x.Score == scores.Min(x => x.Score))
                     .OrderBy(rm => rm.Path.Split('.').Length)
                     .First();

                dico.Add(id, bestScore.Path);
            }
            return dico;
        }
    }
}
