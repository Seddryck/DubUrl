using DubUrl.Adomd.Discovery;
using DubUrl.Parsing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Implementation;
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

internal abstract class SsasRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source);
        var endpoint = p.Take("Data Source");
        var instance = endpoint.Split('\\');
        if (instance.Length > 2) throw new NotSupportedException();
        var hostPort = instance[0].Split(':');
        p.Host = hostPort[0];
        if (hostPort.Length == 2) p.Port = int.Parse(hostPort[1], System.Globalization.CultureInfo.InvariantCulture);
        if (instance.Length == 2) p.Segments.Add(instance[1]);
        else p.Segments.Add("~");
        p.Segments.Add(p.Take("Database", "Initial Catalog"));
        var cube = p.Take("Cube");
        if (cube.Length > 0) p.Segments.Add(cube);
        p.TakeCredentials("User ID", "Password");
        // The existing SSAS forward mapper always emits SSPI using unique assignment.
        if (!string.Equals(p.Take("Integrated Security"), "sspi", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException();
        return p.ToUri(scheme);
    }

    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string CUBE_KEYWORD = "Cube";
    protected internal const string USERNAME_KEYWORD = "User ID";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    public SsasRewriter(DbConnectionStringBuilder csb)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                  new DataSourceMapper(),
                  new AuthentificationMapper(),
                  new InitialCatalogMapper(),
                  new CubeMapper(),
                  new OptionsMapper(),
              ]
        )
    { }

    protected internal class DataSourceMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            var fullHost = new StringBuilder();
            fullHost.Append(urlInfo.Host);
            if (urlInfo.Segments.Count == 0 || urlInfo.Segments.Count > 3)
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' is expecting one to three segments. This connection-url is containing {urlInfo.Segments.Count} segments: '{string.Join("', '", urlInfo.Segments)}'");
            if (urlInfo.Port != 0)
            {
                fullHost.Append(':').Append(urlInfo.Port);
                if (!urlInfo.Segments.First().Equals("~", StringComparison.Ordinal))
                    throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' cannot specify both a port and an instance name. This connection-url is containing the following port {urlInfo.Port} and instance name '{urlInfo.Segments.First()}'. To specify the default instance use the '~' character.");
            }
            if (urlInfo.Segments.Count > 1 && !urlInfo.Segments.First().Equals("~"))
                fullHost.Append('\\').Append(urlInfo.Segments.First());

            Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
        }
    }

    protected internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

            Specificator.Execute(SSPI_KEYWORD, "sspi");
        }
    }

    protected internal class InitialCatalogMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (urlInfo.Segments.Count == 1 && !urlInfo.Segments.First().Equals("~"))
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
            else if (urlInfo.Segments.Count == 1)
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' must specify a Database/Initial Catalog. This connection-url is containing a reference to the default instance ('~') but not to a database/initial catalog.");
            else if (urlInfo.Segments.Count >= 2 && urlInfo.Segments.Count <= 3)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Skip(1).First());
            else
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' is expecting one to three segments. This connection-url is containing {urlInfo.Segments.Count} segments: '{string.Join("', '", urlInfo.Segments)}'");
        }
    }

    protected internal class CubeMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (urlInfo.Segments.Count == 3)
                Specificator.Execute(CUBE_KEYWORD, urlInfo.Segments.Last());
        }
    }
}
