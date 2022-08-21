using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using DubUrl.Mapping.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider.Implementation
{
    [AlternativeProvider<MssqlNCliProviderRegex, OleDbMapper, MsSqlServerDatabase>()]
    internal class MssqlNCliProviderLocator : BaseProviderLocator
    {
        internal class MssqlNCliProviderRegex : BaseProviderRegex
        {
            public MssqlNCliProviderRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("SQLNCLI"),
                    new VersionCapture<VersionOption>(),
                })
            { }
        }
        private const string REGEX_PATTERN = "^[S][Q][L][N][C][L][I]([0-9]*)$";
        private readonly Dictionary<string, int> Candidates = new();

        public MssqlNCliProviderLocator()
            : base(GetRegexPattern<MssqlNCliProviderLocator>()) { }

        internal MssqlNCliProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MssqlNCliProviderLocator>(), providerLister) { }

        internal MssqlNCliProviderLocator(string value)
            : base(GetRegexPattern<MssqlNCliProviderLocator>(), new ExtendedPropertiesMapper(new[] { value })) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider, int.Parse(matches[0]));
        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
}
