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
    internal class QuestDbRewriter : PostgresqlRewriter
    {
        public const string COMMAND_TIMEOUT = "Command Timeout";
        public const string STATEMENT_TIMEOUT = "statement_timeout";
        public const string SERVER_COMPATIBILITY_MODE = "ServerCompatibilityMode";
        public const string NO_TYPE_LOADING = "NoTypeLoading";
        public const string DEFAULT_DATABASE = "qdb";

        public QuestDbRewriter(DbConnectionStringBuilder csb)
            : base(new Specificator(csb),
                      new BaseTokenMapper[] {
                        new HostMapper(),
                        new PortMapper(),
                        new AuthentificationMapper(),
                        new DatabaseMapper(),
                        new OptionsMapper(),
                      }
            )
        { }

        internal new class PortMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
                => Specificator.Execute(PORT_KEYWORD, urlInfo.Port > 0 ? urlInfo.Port : 8812);
        }

        internal new class DatabaseMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 0)
                    Specificator.Execute(DATABASE_KEYWORD, DEFAULT_DATABASE);
                else if (urlInfo.Segments.Length == 1)
                    if (urlInfo.Segments[0] == DEFAULT_DATABASE || urlInfo.Segments[0] == string.Empty)
                        Specificator.Execute(DATABASE_KEYWORD, DEFAULT_DATABASE);
                    else
                        throw new InvalidConnectionUrlException($"One a segment is provided in the connectionUrl it must be '{DEFAULT_DATABASE}'. The segment was '{urlInfo.Segments[0]}'");
                else
                    throw new InvalidConnectionUrlException($"QuestDb is expecting a maximum of 1 segment and it should be '{DEFAULT_DATABASE}'. The connectionUrl has {urlInfo.Segments.Length} segments: '{string.Join(", ", urlInfo.Segments)}'");
            }
        }

        internal new class AuthentificationMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (string.IsNullOrEmpty(urlInfo.Username) || string.IsNullOrEmpty(urlInfo.Password))
                    throw new InvalidConnectionUrlException($"Username and Password are mandatory for QuestDb.");
             
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
            }
        }

        internal class OptionsMapper : Tokening.OptionsMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                var unexpected = urlInfo.Options.Keys.Except(new[] { STATEMENT_TIMEOUT, COMMAND_TIMEOUT, SERVER_COMPATIBILITY_MODE });
                if (unexpected.Any())
                    throw new InvalidConnectionUrlException($"QuestDb is accepting an option named '{STATEMENT_TIMEOUT}' (or its alias '{COMMAND_TIMEOUT}') and also an option named '{SERVER_COMPATIBILITY_MODE}'. The options '{string.Join("', '", unexpected)}' are not supported.");
                else if (urlInfo.Options.ContainsKey(STATEMENT_TIMEOUT) && urlInfo.Options.ContainsKey(COMMAND_TIMEOUT))
                    throw new InvalidConnectionUrlException($"QuestDb is accepting an option named '{STATEMENT_TIMEOUT}' or its alias '{COMMAND_TIMEOUT}' and also an option named '{SERVER_COMPATIBILITY_MODE}'. You cannot specify both of '{COMMAND_TIMEOUT}' and '{STATEMENT_TIMEOUT}'.");
                else
                {
                    if (!urlInfo.Options.ContainsKey(SERVER_COMPATIBILITY_MODE))
                        urlInfo.Options.Add(SERVER_COMPATIBILITY_MODE, NO_TYPE_LOADING);
                    else if (urlInfo.Options[SERVER_COMPATIBILITY_MODE] != NO_TYPE_LOADING)
                        throw new InvalidConnectionUrlException($"QuestDb is accepting a single value '{NO_TYPE_LOADING}' for the option named '{SERVER_COMPATIBILITY_MODE}'. The value '{urlInfo.Options[SERVER_COMPATIBILITY_MODE]}' is not supported.");
                    
                    if (urlInfo.Options.TryGetValue(STATEMENT_TIMEOUT, out string? timeout))
                    {
                        urlInfo.Options.Add(COMMAND_TIMEOUT, timeout);
                        urlInfo.Options.Remove(STATEMENT_TIMEOUT);
                    } 
                    base.Execute(urlInfo);
                }                    
            }
        }
    }
}
