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
        protected internal const string SERVER_COMPATIBILITY_MODE = "ServerCompatibilityMode";
        protected internal const string NO_TYPE_LOADING = "NoTypeLoading";
        protected internal const string NO_RESET_ON_CLOSE = "No Reset On Close";
        protected internal const string NO_RESET_ON_CLOSE_DEFAULT = "true";
        protected internal const string DEFAULT_LOCALHOST = "localhost";
        protected internal const string DEFAULT_REMOTE_HOST = "proxy.glaredb.com";
        protected internal const int DEFAULT_PORT = 6543;

        public GlareDbRewriter(DbConnectionStringBuilder csb)
            : base(   new Specificator(csb),
                      new BaseTokenMapper[] {
                        new HostMapper(),
                        new PortMapper(),
                        new AuthentificationMapper(),
                        new PostgresqlRewriter.DatabaseMapper(),
                        new OptionsMapper(),
                      }
            )
        { }

        private static bool IsLocalHost(UrlInfo urlInfo)
            => urlInfo.Host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)
                || urlInfo.Host.Equals(".", StringComparison.InvariantCultureIgnoreCase)
                || urlInfo.Host.Equals("127.0.0.1", StringComparison.InvariantCultureIgnoreCase);

        protected GlareDbRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
            : base(specificator, tokenMappers) { }

        internal class HostMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (IsLocalHost(urlInfo))
                    Specificator.Execute(SERVER_KEYWORD, DEFAULT_LOCALHOST);
                else if (urlInfo.Host.Contains('.'))
                    Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
                else
                    Specificator.Execute(SERVER_KEYWORD, $"{urlInfo.Host}.{DEFAULT_REMOTE_HOST}");
            }
        }

        internal class PortMapper : PostgresqlRewriter.PortMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Port==0)
                    Specificator.Execute(PORT_KEYWORD, DEFAULT_PORT);
                else
                    base.Execute(urlInfo);
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
                if (
                        (
                            string.IsNullOrEmpty(urlInfo.Username)
                            || string.IsNullOrEmpty(urlInfo.Password)
                        ) && (!IsLocalHost(urlInfo))
                    )
                    throw new InvalidConnectionUrlException("If the host is not localhost then username and password are mandatory for GlareDB connection url.");                    
            }
        }

        internal class OptionsMapper : Tokening.OptionsMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                var unexpected = urlInfo.Options.Keys.Except(new[] { SERVER_COMPATIBILITY_MODE, NO_RESET_ON_CLOSE });
                if (unexpected.Any())
                    throw new InvalidConnectionUrlException($"GlareDb is accepting options named '{SERVER_COMPATIBILITY_MODE}' and '{NO_RESET_ON_CLOSE}'. The options '{string.Join("', '", unexpected)}' are not supported.");
                else
                {
                    if (!urlInfo.Options.ContainsKey(SERVER_COMPATIBILITY_MODE))
                        urlInfo.Options.Add(SERVER_COMPATIBILITY_MODE, NO_TYPE_LOADING);
                    if (!urlInfo.Options.ContainsKey(NO_RESET_ON_CLOSE))
                        urlInfo.Options.Add(NO_RESET_ON_CLOSE, NO_RESET_ON_CLOSE_DEFAULT);

                    if (urlInfo.Options[SERVER_COMPATIBILITY_MODE] != NO_TYPE_LOADING)
                        throw new InvalidConnectionUrlException($"GlareDb is accepting a single value '{NO_TYPE_LOADING}' for the option named '{SERVER_COMPATIBILITY_MODE}'. The value '{urlInfo.Options[SERVER_COMPATIBILITY_MODE]}' is not supported.");
                    if (urlInfo.Options[NO_RESET_ON_CLOSE] != NO_RESET_ON_CLOSE_DEFAULT)
                        throw new InvalidConnectionUrlException($"GlareDb is accepting a single value '{NO_RESET_ON_CLOSE_DEFAULT}' for the option named '{NO_RESET_ON_CLOSE}'. The value '{urlInfo.Options[NO_RESET_ON_CLOSE]}' is not supported.");
                    base.Execute(urlInfo);
                }
            }
        }
    }
}