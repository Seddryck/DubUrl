using DubUrl.Provider.Oracle.Metadata;
using DubUrl.Locating.RegexUtils;
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
            [new OptionsMapper(), new OracleEndpointMapper()])
    { }

    internal OracleProviderLocator(ProviderLister providerLister)
        : base(GetRegexPattern<OracleProviderLocator>(), providerLister)
    { }

    protected override void AddCandidate(string provider, string[] matches)
        => _candidates.Add(provider);

    protected override List<string> RankCandidates()
        => _candidates;

    internal sealed class OracleEndpointMapper : BaseTokenMapper
    {
        public override void Execute(DubUrl.Parsing.ConnectionUri uri)
        {
            if (uri.Segments.Count > 1) throw new ArgumentException("Oracle OLE DB expects one service name.");
            var source = uri.Segments.Count == 0 ? uri.Host
                : $"//{uri.Host}:{(uri.Port == 0 ? 1521 : uri.Port)}/{uri.Segments[0]}";
            Specificator.Execute("Data Source", source);
        }
    }
}
