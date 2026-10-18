using DubUrl.Adomd.Discovery;
using DubUrl.Parsing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace DubUrl.Adomd.Rewriting;

internal class PowerBiDesktopRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source);
        var endpoint = p.Take("Data Source");
        if (!Uri.TryCreate("tcp://" + endpoint, UriKind.Absolute, out var address) || address.Port <= 0)
            throw new NotSupportedException();
        p.Host = address.Host;
        p.Port = address.Port;
        return p.ToUri(scheme);
    }

    private const string EXCEPTION_DATABASE_NAME = "Power BI Desktop";
    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string DEFAULT_LOCALHOST = "localhost";
    protected internal static readonly string[] VALID_HOSTS =
        ["127.0.0.1", ".", string.Empty, DEFAULT_LOCALHOST];

    public PowerBiDesktopRewriter(DbConnectionStringBuilder csb)
        : this(csb, new PowerBiDiscoverer())
    { }

    protected internal PowerBiDesktopRewriter(DbConnectionStringBuilder csb, IPowerBiDiscoverer discoverer)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new DataSourceMapper(discoverer)
              ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        private IPowerBiDiscoverer Discoverer { get; }

        public DataSourceMapper(IPowerBiDiscoverer discoverer)
            => Discoverer = discoverer;

        public override void Execute(ConnectionUri urlInfo)
        {
            if (!VALID_HOSTS.Any(x => x.Equals(urlInfo.Host, StringComparison.InvariantCultureIgnoreCase)))
                throw new InvalidConnectionUrlException("Host must be 'localhost' or equivalent when using a Power BI Desktop connection-url");

            var port = urlInfo.Port == 0 
                ? GetPortFromSegments(urlInfo.Segments) 
                : urlInfo.Segments.Count==0
                    ? urlInfo.Port
                    : throw new InvalidConnectionUrlException("You cannot define the port and a segment in a Power BI Desktop connection-url");

            Specificator.Execute(SERVER_KEYWORD, $"{DEFAULT_LOCALHOST}:{port}");
        }

        public int GetPortFromSegments(IReadOnlyList<string> segments)
        {
            if(segments==null || segments.Count==0)
                throw new InvalidConnectionUrlMissingSegmentsException(EXCEPTION_DATABASE_NAME);

            var pbiName = segments.Count == 1
                            ? segments[0]
                            : throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, segments);

            var processes = Discoverer.GetPowerBiProcesses();
            if (processes.Any(x => x.Name == pbiName))
                return processes.Single(x => x.Name == pbiName).Port;
            throw new InvalidConnectionUrlException($"Cannot find any process where the window title is '{pbiName}'.");
        }
    }
}
