using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.OleDb.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.OleDb.Providers;

[Provider<MssqlOleDbProviderRegex, OleDbMapper, MsSqlServerDatabase>()]
public class MssqlOleDbProviderLocator : BaseProviderLocator
{
    internal class MssqlOleDbProviderRegex : BaseProviderRegex
    {
        public MssqlOleDbProviderRegex()
            : base(
            [
                new WordMatch("MSOLEDBSQL"),
            ])
        { }
    }
    private List<string> Candidates { get; } = [];

    public MssqlOleDbProviderLocator()
        : base(GetRegexPattern<MssqlOleDbProviderLocator>(),
            [new OptionsMapper()
                , new OleDbRewriter.InitialCatalogMapper()
                , new OleDbRewriter.ServerMapper()
            ]
        )
    { }

    internal MssqlOleDbProviderLocator(ProviderLister providerLister)
        : base(GetRegexPattern<MssqlOleDbProviderLocator>(), providerLister) { }

    protected override void AddCandidate(string provider, string[] matches)
        => Candidates.Add(provider);
    protected override List<string> RankCandidates()
        => Candidates;
}
