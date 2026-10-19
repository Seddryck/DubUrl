using DubUrl.Parsing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace DubUrl.Adomd.Rewriting;

internal class AsAzureRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source);
        var endpoint = new Uri(p.Take("Data Source"), UriKind.Absolute);
        if (endpoint.Scheme is not ("asazure" or "link")) throw new NotSupportedException();
        p.Host = endpoint.Host;
        p.Segments.AddRange(endpoint.Segments.Skip(1).Select(s => Uri.UnescapeDataString(s.TrimEnd('/'))).Where(s => s.Length > 0));
        var cube = p.Take("Cube");
        if (cube.Length > 0) p.Segments.Add(cube);
        return p.ToUri(scheme);
    }

    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string CUBE_KEYWORD = "Cube";
    protected internal const string ASAZURE_SCHEME = "asazure";
    protected internal const string LINK_SCHEME = "link";
    protected internal const string DEFAULT_ASAZURE_HOST = "asazure.windows.net";

    public AsAzureRewriter(DbConnectionStringBuilder csb)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                  new DataSourceMapper(),
                  new CubeMapper()
              ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            var fullHost = new StringBuilder();
            var segments = urlInfo.Segments.ToList();

            var isAsAzure = IsAsAzure(urlInfo);

            fullHost.Append(isAsAzure ? ASAZURE_SCHEME : LINK_SCHEME)
            .Append(Uri.SchemeDelimiter)
            .Append(urlInfo.Host);

            if (isAsAzure)
            {
                if (urlInfo.Host.Split('.').Length == 1)
                    fullHost.Append('.').Append(DEFAULT_ASAZURE_HOST);

                if (urlInfo.Segments.Count == 0)
                    throw new InvalidConnectionUrlException($"Missing, at least, the name of the instance for a Azure Analysis Services.");

                fullHost.Append('/').Append(Encode(segments.First()));
            }
            else
            {
                if (!urlInfo.Host.EndsWith('/'))
                    fullHost.Append('/');
            }

            Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
        }

        protected virtual bool IsAsAzure(ConnectionUri urlInfo)
            => urlInfo.Host.Split('.').Length == 1
                || urlInfo.Host.EndsWith(DEFAULT_ASAZURE_HOST, StringComparison.InvariantCultureIgnoreCase);

        protected virtual string Encode(string value)
            => Uri.UnescapeDataString(value).Length < value.Length
                ? value
                : Uri.EscapeDataString(value);
    }


    protected internal class CubeMapper : DataSourceMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            var isAsAzure = IsAsAzure(urlInfo);
            if (isAsAzure)
            {
                if (urlInfo.Segments.Count > 2)
                    throw new InvalidConnectionUrlException($"A connection-url to Azure Analysis Services must contain at least one segment and maximum two. Current value contains {urlInfo.Segments.Count} segments: '{string.Join(", ", urlInfo.Segments)}'");
                if (urlInfo.Segments.Count == 2)
                    Specificator.Execute(CUBE_KEYWORD, urlInfo.Segments.Last());
            }
            else
            {
                if (urlInfo.Segments.Count > 1)
                    throw new InvalidConnectionUrlException($"A connection-url to Azure Analysis Services using server name alias must contain zero to one segment. The segment represents the cube. Current value contains {urlInfo.Segments.Count} segments: '{string.Join(", ", urlInfo.Segments)}'");
                else if (urlInfo.Segments.Count == 1)
                    Specificator.Execute(CUBE_KEYWORD, urlInfo.Segments.Last());
            }
        }
    }
}
