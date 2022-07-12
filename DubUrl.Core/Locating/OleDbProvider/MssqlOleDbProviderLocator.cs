using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    internal class MssqlOleDbProviderLocator : BaseProviderLocator
    {
        private const string REGEX_PATTERN = "^\\bMSOLEDBSQL\\b$";
        private readonly List<string> Candidates = new();

        public MssqlOleDbProviderLocator()
            : base(REGEX_PATTERN) { }
        internal MssqlOleDbProviderLocator(ProviderLister providerLister)
            : base(REGEX_PATTERN, providerLister) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}
