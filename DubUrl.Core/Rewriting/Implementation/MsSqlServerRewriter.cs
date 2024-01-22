using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

public class MsSqlServerRewriter : ConnectionStringRewriter
{
    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string DATABASE_KEYWORD = "Initial Catalog";
    protected internal const string USERNAME_KEYWORD = "User ID";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    protected MsSqlServerRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers)
    { }

    public MsSqlServerRewriter(DbConnectionStringBuilder csb)
        : this(new Specificator(csb),
              [
                new DataSourceMapper(),
                new AuthentificationMapper(),
                new InitialCatalogMapper(),
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
            if (urlInfo.Segments.Length == 2)
                fullHost.Append('\\').Append(urlInfo.Segments.First());
            if (urlInfo.Port != 0)
                fullHost.Append(',').Append(urlInfo.Port);

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

            if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(SSPI_KEYWORD, "sspi");
            else
                Specificator.Execute(SSPI_KEYWORD, false);
        }
    }

    protected internal class InitialCatalogMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length > 0 && urlInfo.Segments.Length <= 2)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Last());
            else
                throw new InvalidConnectionUrlException($"The connection-url for Microsoft SQL Server is expecting one or two segments. This connection-url is containing {urlInfo.Segments.Length} segments: '{string.Join("', '", [.. urlInfo.Segments])}'");
        }
    }
}
