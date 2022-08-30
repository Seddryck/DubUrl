using DubUrl.OleDb;
using DubUrl.Locating.Options;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Tokening;
using DubUrl.OleDb.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;

namespace DubUrl.OleDb.Providers
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

        private readonly Dictionary<string, int> Candidates = new();

        public MssqlNCliProviderLocator()
            : base(GetRegexPattern<MssqlNCliProviderLocator>(), new BaseTokenMapper[]
                { new BaseMapper.OptionsMapper()
                    , new OleDbMapper.InitialCatalogMapper()
                    , new OleDbMapper.ServerMapper()
                }
            )
        { }

        internal MssqlNCliProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MssqlNCliProviderLocator>(), providerLister) { }

        internal MssqlNCliProviderLocator(string value)
            : base(GetRegexPattern<MssqlNCliProviderLocator>(), new BaseTokenMapper[]
                { new BaseMapper.OptionsMapper()
                    , new OleDbMapper.InitialCatalogMapper()
                    , new OleDbMapper.ServerMapper()
                }
            )
        { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider, int.Parse(matches[0]));
        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
    }
}
