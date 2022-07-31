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
        "PostgreSQL"
        , new[] { "pg", "pgsql", "postgres", "postgresql" }
        , "Npgsql"
    )]
    internal class PgsqlMapper : BaseMapper
    {
        protected internal const string SERVER_KEYWORD = "Host";
        protected internal const string PORT_KEYWORD = "Port";
        protected internal const string DATABASE_KEYWORD = "Database";
        protected internal const string USERNAME_KEYWORD = "Username";
        protected internal const string PASSWORD_KEYWORD = "Password";
        protected internal const string SSPI_KEYWORD = "Integrated Security";

        public PgsqlMapper(DbConnectionStringBuilder csb)
            : base(csb,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new HostMapper(),
                    new AuthentificationMapper(),
                    new DatabaseMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class HostMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
                if (urlInfo.Port > 0)
                    Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
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

                if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(SSPI_KEYWORD, true);
                else
                    Specificator.Execute(SSPI_KEYWORD, false);
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
