using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class MySqlDataRewriter : MySqlConnectorRewriter
{
    protected internal new const string SERVER_KEYWORD = "server";
    protected internal new const string DATABASE_KEYWORD = "database";
    protected internal new const string USERNAME_KEYWORD = "user id";
    protected internal new const string PASSWORD_KEYWORD = "password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    public MySqlDataRewriter(DbConnectionStringBuilder csb)
        : base(new Specificator(csb),
              [
                new ServerMapper(),
                new AuthentificationMapper(),
                new DatabaseMapper(),
                new OptionsMapper(),
              ]
        )
    { }

    internal new class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

            if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(SSPI_KEYWORD, "sspi");
        }
    }
}
