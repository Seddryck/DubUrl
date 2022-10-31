using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceMatching
{
    public abstract class BaseResourceMatcher : IResourceMatcher
    {
        protected static string? FirstOrDefault(IEnumerable<string> candidates, string[] resourceNames)
            => candidates.FirstOrDefault(x => resourceNames.Any(y => x.Equals(y, StringComparison.InvariantCultureIgnoreCase)));

        public abstract string? Execute(string id, string[] resourceNames);
    }
}
