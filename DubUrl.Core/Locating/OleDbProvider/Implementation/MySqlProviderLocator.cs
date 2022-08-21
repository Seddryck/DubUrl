using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.Mapping.Implementation;
using DubUrl.Mapping.Tokening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider.Implementation
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
        private readonly List<string> Candidates = new();

        public MySqlProviderLocator()
            : base(GetRegexPattern<MySqlProviderLocator>()) { }

        internal MySqlProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MySqlProviderLocator>(), providerLister) { }

        internal MySqlProviderLocator(string value)
            : base(GetRegexPattern<MySqlProviderLocator>(), new ExtendedPropertiesMapper(new[] { value })) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}
