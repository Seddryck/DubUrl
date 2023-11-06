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

            var resources = ResourceNames.Where(x => x.StartsWith(directory) && x.EndsWith(extension));
            var ids = resources.Select(x => x.Split(".")[..^2].Last()).Distinct();

            foreach (var id in ids)
            {
                var candidates = resources.Where(x => x.Split(".")[..^2].Last() == id);

                var scores = Enumerable.Empty<ResourceMatch>()
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{connectivity}.{dialect}.", StringComparison.InvariantCultureIgnoreCase))
                                                              && !string.IsNullOrEmpty(connectivity))
                                      .Select(candidate => new ResourceMatch(candidate, 0))
                           )
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{connectivity}.{FallbackPath}.", StringComparison.InvariantCultureIgnoreCase))
                                                              && !string.IsNullOrEmpty(connectivity))
                                      .Select(candidate => new ResourceMatch(candidate, 10))
                           )
                     .Union(candidates.Where(candidate => candidate.Contains($".{connectivity}.", StringComparison.InvariantCultureIgnoreCase)
                                                              && !string.IsNullOrEmpty(connectivity))
                                      .Select(candidate => new ResourceMatch(candidate, 30))
                           )
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{dialect}.", StringComparison.InvariantCultureIgnoreCase)))
                                      .Select(candidate => new ResourceMatch(candidate, 50))
                           )
                     .Union(candidates.Where(candidate => dialects.Any(dialect => candidate.Contains($".{FallbackPath}.", StringComparison.InvariantCultureIgnoreCase)))
                                      .Select(candidate => new ResourceMatch(candidate, 70))
                           )
                     .Union(candidates.Select(candidate => new ResourceMatch(candidate, 100)))
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

        public virtual IDictionary<string, object?> ReadDictionary(string resourceName)
        {
            var dico = new Dictionary<string, object?>();
            using var reader = GetResourceReader(resourceName);
            while (reader.Peek() >= 0)
            {
                (var key, var value) = ParseDictionaryEntry(reader.ReadLine());
                if (!string.IsNullOrEmpty(key))
                    dico.Add(key, value);
            }
            return dico;
        }

        protected virtual TextReader GetResourceReader(string resourceName)
            => new StreamReader(ResouceAssembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException(resourceName));

        protected virtual (string?, object?) ParseDictionaryEntry(string? entry)
        {
            if (string.IsNullOrEmpty(entry))
                return (null, null);
            var separator = entry.IndexOf(':');
            if (separator == -1)
                return (null, null);
            var key = entry[..separator].Trim();
            if (key[0]== '\"' && key[^1]=='\"')
                key = key.Trim('\"');

            var rawValue = entry[(separator+1)..].Trim();
            if (rawValue[0] == '\"' && rawValue[^1] == '\"')
                return (key, rawValue.Trim('\"'));
            else if (rawValue.All(char.IsDigit))
                return (key, int.Parse(rawValue));
            else if (rawValue.All(c => char.IsDigit(c) || c == '.'))
                return (key, decimal.Parse(rawValue));
            else if (rawValue.Equals("true", StringComparison.InvariantCultureIgnoreCase) || rawValue.Equals("false", StringComparison.InvariantCultureIgnoreCase))
                return (key, bool.Parse(rawValue));
            else
                throw new ArgumentOutOfRangeException(nameof(entry));
        }
    }
}
