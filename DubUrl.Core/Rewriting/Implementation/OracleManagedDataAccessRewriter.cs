using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation
{
    internal class OracleManagedDataAccessRewriter : ConnectionStringRewriter
    {
        private const string EXCEPTION_DATABASE_NAME = "Oracle Managed Data Access";
        protected internal const string DATASOURCE_KEYWORD = "DATA SOURCE";
        protected internal const string SERVER_KEYWORD = "HOST";
        protected internal const string PORT_KEYWORD = "PORT";
        protected internal const string DATABASE_KEYWORD = "SERVICE_NAME";
        protected internal const string USERNAME_KEYWORD = "USER ID";
        protected internal const string PASSWORD_KEYWORD = "PASSWORD";

        public OracleManagedDataAccessRewriter(DbConnectionStringBuilder csb)
            : base(new Specificator(csb),
                  new BaseTokenMapper[] {
                    new DsnMapper(),
                    new AuthentificationMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class AuthentificationMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
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
            public override void Execute(UrlInfo urlInfo)
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
                    throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
            }
        }
    }
}
