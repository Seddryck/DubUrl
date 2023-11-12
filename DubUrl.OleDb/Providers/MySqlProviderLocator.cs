using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.OleDb.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.OleDb.Providers
{
    [Provider<MySqlProviderRegex, OleDbMapper, MySqlDatabase>()]
    internal class MySqlProviderLocator : BaseProviderLocator
    {
        internal class MySqlProviderRegex : BaseProviderRegex
        {
            public MySqlProviderRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MySQL Provider"),
                })
            { }
        }
        private List<string> Candidates { get; } = new();

        public MySqlProviderLocator()
            : base(GetRegexPattern<MySqlProviderLocator>(), new BaseTokenMapper[]
                { new OptionsMapper()
                    , new OleDbRewriter.InitialCatalogMapper()
                    , new OleDbRewriter.ServerMapper()
                }
            )
        { }

        internal MySqlProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MySqlProviderLocator>(), providerLister) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}
