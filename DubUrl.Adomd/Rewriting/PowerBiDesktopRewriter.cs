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

namespace DubUrl.Adomd.Rewriting
{
    internal class PowerBiDesktopRewriter : ConnectionStringRewriter
    {
        protected internal const string SERVER_KEYWORD = "Data Source";

        protected internal const string DEFAULT_LOCALHOST = "localhost";
        protected internal readonly static string[] VALID_HOSTS =
            new[] { "127.0.0.1", ".", string.Empty, DEFAULT_LOCALHOST };

        public PowerBiDesktopRewriter(DbConnectionStringBuilder csb)
            : this(csb, new PowerBiDiscoverer())
        { }

        protected internal PowerBiDesktopRewriter(DbConnectionStringBuilder csb, IPowerBiDiscoverer discoverer)
            : base(new SpecificatorUnchecked(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(discoverer)
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            private IPowerBiDiscoverer Discoverer { get; }

            public DataSourceMapper(IPowerBiDiscoverer discoverer)
                => Discoverer = discoverer;

            public override void Execute(UrlInfo urlInfo)
            {
                if (!VALID_HOSTS.Any(x => x.Equals(urlInfo.Host, StringComparison.InvariantCultureIgnoreCase)))
                    throw new InvalidConnectionUrlException("Host must be 'localhost' or equivalent when using a Power BI Desktop connection-url");

                var port = urlInfo.Port == 0 
                    ? GetPortFromSegments(urlInfo.Segments) 
                    : urlInfo.Segments.Length==0
                        ? urlInfo.Port
                        : throw new InvalidConnectionUrlException("You cannot define the port and a segment in a Power BI Desktop connection-url");

                Specificator.Execute(SERVER_KEYWORD, $"{DEFAULT_LOCALHOST}:{port}");
            }

            public int GetPortFromSegments(string[] segments)
            {
                var pbiName = segments.Length == 1
                                ? segments[0]
                                : throw new InvalidConnectionUrlTooManySegmentsException("Power BI Desktop");

                var processes = Discoverer.GetPowerBiProcesses();
                if (processes.Any(x => x.Name == pbiName))
                    return processes.Single(x => x.Name == pbiName).Port;
                throw new InvalidConnectionUrlException($"Cannot find any process where the window title is '{pbiName}'.");
            }
        }
    }
}