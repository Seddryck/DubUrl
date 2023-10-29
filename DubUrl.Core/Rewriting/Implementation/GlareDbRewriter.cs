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
    internal class GlareDbRewriter : ConnectionStringRewriter
    {
        protected internal const string SERVER_KEYWORD = "Host";
        protected internal const string PORT_KEYWORD = "Port";
        protected internal const string DATABASE_KEYWORD = "Database";
        protected internal const string USERNAME_KEYWORD = "Username";
        protected internal const string PASSWORD_KEYWORD = "Password";

        public GlareDbRewriter(DbConnectionStringBuilder csb)
            : base(   new Specificator(csb),
                      new BaseTokenMapper[] {
                        new PostgresqlRewriter.HostMapper(),
                        new PostgresqlRewriter.PortMapper(),
                        new AuthentificationMapper(),
                        new PostgresqlRewriter.DatabaseMapper(),
                      }
            )
        { }

        protected GlareDbRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
            : base(specificator, tokenMappers) { }

        internal class AuthentificationMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (!string.IsNullOrEmpty(urlInfo.Username))
                    Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                if (!string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
                if (
                        (
                            string.IsNullOrEmpty(urlInfo.Username) 
                            || string.IsNullOrEmpty(urlInfo.Password)
                        ) && (
                            !urlInfo.Host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)
                            && !urlInfo.Host.Equals(".", StringComparison.InvariantCultureIgnoreCase)
                            && !urlInfo.Host.Equals("127.0.0.1", StringComparison.InvariantCultureIgnoreCase)
                        )
                    )
                    throw new InvalidConnectionUrlException("If the host is not localhost then username and password are mandatory for GlareDG connection url.");                    
            }
        }
    }
}