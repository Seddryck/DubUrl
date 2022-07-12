using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class MssqlMapper : BaseMapper
    {
        private const string SERVER_KEYWORD = "Data source";
        private const string DATABASE_KEYWORD = "Initial catalog";
        private const string USERNAME_KEYWORD = "User id";
        private const string PASSWORD_KEYWORD = "Password";
        private const string SSPI_KEYWORD = "Integrated security";

        public MssqlMapper(DbConnectionStringBuilder csb)
            : base(csb, 
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new AuthentificationMapper(),
                    new InitialCatalogMapper(),
                    new OptionsMapper(),
                  }
            ) { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
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
            internal override void Execute(UrlInfo urlInfo)
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
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length > 0 && urlInfo.Segments.Count() <= 2)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Last());
                else
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
