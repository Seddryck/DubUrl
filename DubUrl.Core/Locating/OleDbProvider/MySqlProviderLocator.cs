using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    internal class MySqlProviderLocator : BaseProviderLocator
    {
        private const string REGEX_PATTERN = "^\\bMySQL Provider\\b$";
        private readonly List<string> Candidates = new();

        public MySqlProviderLocator()
            : base(REGEX_PATTERN) { }

        internal MySqlProviderLocator(ProviderLister providerLister)
            : base(REGEX_PATTERN, providerLister) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}
