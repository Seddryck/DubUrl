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

internal class SsasMultidimRewriter : ConnectionStringRewriter
{
    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string CUBE_KEYWORD = "Cube";
    protected internal const string USERNAME_KEYWORD = "User ID";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    public SsasMultidimRewriter(DbConnectionStringBuilder csb)
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
        public override void Execute(UrlInfo urlInfo)
        {
            var fullHost = new StringBuilder();
            fullHost.Append(urlInfo.Host);
            if (urlInfo.Segments.Length == 0 || urlInfo.Segments.Length > 3)
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' is expecting one to three segments. This connection-url is containing {urlInfo.Segments.Length} segments: '{string.Join("', '", [.. urlInfo.Segments])}'");
            if (urlInfo.Port != 0)
            {
                fullHost.Append(':').Append(urlInfo.Port);
                if (!urlInfo.Segments.First().Equals('~'))
                    throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' cannot specify both a port and an instance name. This connection-url is containing the following port {urlInfo.Port} and instance name '{urlInfo.Segments.First()}'. To specify the default instance use the '~' character.");
            }
            if (urlInfo.Segments.Length > 1 && !urlInfo.Segments.First().Equals("~"))
                fullHost.Append('\\').Append(urlInfo.Segments.First());

            Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
        }
    }

    protected internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
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
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length == 1 && !urlInfo.Segments.First().Equals("~"))
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
            else if (urlInfo.Segments.Length == 1)
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' must specify a Database/Initial Catalog. This connection-url is containing a reference to the default instance ('~') but not to a database/initial catalog.");
            else if (urlInfo.Segments.Length >= 2 && urlInfo.Segments.Length <= 3)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Skip(1).First());
            else
                throw new InvalidConnectionUrlException($"The connection-url for 'Microsoft SQL Server Analysis Service Multidimensional' is expecting one to three segments. This connection-url is containing {urlInfo.Segments.Length} segments: '{string.Join("', '", [.. urlInfo.Segments])}'");
        }
    }

    protected internal class CubeMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length == 3)
                Specificator.Execute(CUBE_KEYWORD, urlInfo.Segments.Last());
        }
    }
}
