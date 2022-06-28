using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class OracleMapper : BaseMapper
    {
        private const string SERVER_KEYWORD = "HOST";
        private const string PORT_KEYWORD = "PORT";
        private const string DATABASE_KEYWORD = "SERVICE_NAME";
        private const string USERNAME_KEYWORD = "USER ID";
        private const string PASSWORD_KEYWORD = "PASSWORD";

        public OracleMapper(DbConnectionStringBuilder csb)
            : base(csb,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new DsnMapper(),
                    new AuthentificationMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class AuthentificationMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (!string.IsNullOrEmpty(urlInfo.Username))
                {
                    Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                    if (!string.IsNullOrEmpty(urlInfo.Password))
                        Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
                }
                else
                {
                    Specificator.Execute(USERNAME_KEYWORD, "/");
                    Specificator.Execute(PASSWORD_KEYWORD, string.Empty);
                }
            }
        }

        internal class DsnMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                //If only host is specified, it's the TNS name
                if (urlInfo.Segments.Length == 0 && urlInfo.Port == 0)
                    Specificator.Execute("DATA SOURCE", urlInfo.Host);

                //If segment is specified then it's the ConnectDescriptor
                else if (urlInfo.Segments.Length == 1)
                    Specificator.Execute("DATA SOURCE",
                        $"(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)" +
                        $"({SERVER_KEYWORD}={urlInfo.Host})({PORT_KEYWORD}={(urlInfo.Port > 0 ? urlInfo.Port : 1521)}))(CONNECT_DATA=" +
                        $"({DATABASE_KEYWORD}={urlInfo.Segments.First()})))");
                else
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
