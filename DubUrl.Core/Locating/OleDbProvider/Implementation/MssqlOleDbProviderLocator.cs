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
    [Provider<MssqlOleDbProviderRegex, OleDbMapper, MsSqlServerDatabase>()]
    internal class MssqlOleDbProviderLocator : BaseProviderLocator
    {
        internal class MssqlOleDbProviderRegex : BaseProviderRegex
        {
            public MssqlOleDbProviderRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MSOLEDBSQL"),
                })
            { }
        }
        private readonly List<string> Candidates = new();

        public MssqlOleDbProviderLocator()
            : base(GetRegexPattern<MssqlOleDbProviderLocator>()) { }

        internal MssqlOleDbProviderLocator(ProviderLister providerLister)
            : base(GetRegexPattern<MssqlOleDbProviderLocator>(), providerLister) { }

        internal MssqlOleDbProviderLocator(string value)
            : base(GetRegexPattern<MssqlOleDbProviderLocator>(), new ExtendedPropertiesMapper(new[] { value })) { }

        protected override void AddCandidate(string provider, string[] matches)
            => Candidates.Add(provider);
        protected override List<string> RankCandidates()
            => Candidates;
    }
}
