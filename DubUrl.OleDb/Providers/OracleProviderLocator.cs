using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Database;
using DubUrl.OleDb.Mapping;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.OleDb.Providers;

[Provider<OracleProviderRegex, OleDbMapper, OracleDatabase>()]
public sealed class OracleProviderLocator : BaseProviderLocator
{
    internal sealed class OracleProviderRegex : BaseProviderRegex
    {
        public OracleProviderRegex()
            : base([new WordMatch("OraOLEDB.Oracle")])
        { }
    }

    private readonly List<string> _candidates = [];

    public OracleProviderLocator()
        : base(
            GetRegexPattern<OracleProviderLocator>(),
            [new OptionsMapper(), new OleDbRewriter.ServerMapper()])
    { }

    internal OracleProviderLocator(ProviderLister providerLister)
        : base(GetRegexPattern<OracleProviderLocator>(), providerLister)
    { }

    protected override void AddCandidate(string provider, string[] matches)
        => _candidates.Add(provider);

    protected override List<string> RankCandidates()
        => _candidates;
}
