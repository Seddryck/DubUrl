using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    internal class MssqlNCliProviderLocator : BaseProviderLocator
    {
        private const string REGEX_PATTERN = "^[S][Q][L][N][C][L][I]([0-9]*)$";
        private readonly Dictionary<string, int> Candidates = new();

        public MssqlNCliProviderLocator()
            : base(REGEX_PATTERN) { }

        internal MssqlNCliProviderLocator(ProviderLister providerLister)
            : base(REGEX_PATTERN, providerLister) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider, int.Parse(matches[0]));
        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
}
