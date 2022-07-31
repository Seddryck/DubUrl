using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [Mapper(
        "Teradata"
        , new[] { "td", "teradata", "tera" }
        , "Teradata.Client"
    )]
    internal class TeradataMapper : BaseMapper
    {
        internal const string SERVER_KEYWORD = "Data Source";
        internal const string PORT_KEYWORD = "Port Number";
        internal const string DATABASE_KEYWORD = "Database";
        internal const string USERNAME_KEYWORD = "User Id";
        internal const string PASSWORD_KEYWORD = "Password";
        internal const string SSPI_KEYWORD = "Integrated Security";

        public TeradataMapper(DbConnectionStringBuilder csb)
            : base(csb,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new PortNumberMapper(),
                    new DatabaseMapper(),
                    new AuthentificationMapper(),
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
            }
        }

        internal class PortNumberMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Port > 0)
                    Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 0)
                    return;
                else if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal class AuthentificationMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (!string.IsNullOrEmpty(urlInfo.Username))
                    Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                if (!string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

                if (!urlInfo.Options.ContainsKey(SSPI_KEYWORD))
                    Specificator.Execute(SSPI_KEYWORD,
                        (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password)));
            }
        }
    }
}
