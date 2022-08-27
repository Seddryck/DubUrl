using DubUrl.Mapping.Database;
using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<MsSqlServerDatabase>(
        "System.Data.SqlClient"
    )]
    internal class MsSqlServerMapper : BaseMapper
    {
        protected internal const string SERVER_KEYWORD = "Data Source";
        protected internal const string DATABASE_KEYWORD = "Initial Catalog";
        protected internal const string USERNAME_KEYWORD = "User ID";
        protected internal const string PASSWORD_KEYWORD = "Password";
        protected internal const string SSPI_KEYWORD = "Integrated Security";

        public MsSqlServerMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb, 
                  dialect,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new AuthentificationMapper(),
                    new InitialCatalogMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
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

        internal class AuthentificationMapper : BaseTokenMapper
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

        internal class InitialCatalogMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length > 0 && urlInfo.Segments.Length <= 2)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Last());
                else
                    throw new ArgumentOutOfRangeException($"Expecting one or two segments in the connectionUrl but was {urlInfo.Segments.Length} segments. The list of segments was '{string.Join("', '", urlInfo.Segments.ToArray())}'");
            }
        }
    }
}
