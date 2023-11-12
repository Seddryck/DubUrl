using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Prql
{
    public class EmbeddedPrqlFileResourceManager : EmbeddedSqlFileResourceManager
    {
        public EmbeddedPrqlFileResourceManager(Assembly assembly)
            :base(assembly) { }

        public override bool Any(string id, IResourceMatchingOption option)
        {
            var isMatching = IsMatching(id, option);
            return ResourceNames.Any(isMatching.Invoke);
        }

        public override string BestMatch(string id, IResourceMatchingOption option)
        {
            var isMatching = IsMatching(id, option);
            return ResourceNames.Single(isMatching.Invoke);
        }

        private Func<string, bool> IsMatching(string id, IResourceMatchingOption option)
        {
            var matchingOption = (option as NoneMatchingOption) ?? throw new InvalidCastException(nameof(option));
            bool isMatching(string value) => value.Equals($"{id}.prql", StringComparison.InvariantCultureIgnoreCase);
            return isMatching;
        }
    }
}
