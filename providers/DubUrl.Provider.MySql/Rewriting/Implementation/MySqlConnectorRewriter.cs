using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class MySqlConnectorRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Conversion.Implementation.MySqlConnectionStringReverser.Convert(scheme, source);

    private const string EXCEPTION_DATABASE_NAME = "MySQL";
    protected internal const string SERVER_KEYWORD = "Server";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string USERNAME_KEYWORD = "User ID";
    protected internal const string PASSWORD_KEYWORD = "Password";

    public MySqlConnectorRewriter(DbConnectionStringBuilder csb)
        : this(csb, EXCEPTION_DATABASE_NAME) { }

    protected MySqlConnectorRewriter(DbConnectionStringBuilder csb, string databaseName)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new ServerMapper(),
                new AuthentificationMapper(),
                new DatabaseMapper(databaseName),
                new OptionsMapper(),
              ]
        )
    { }

    protected MySqlConnectorRewriter(Specificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers) { }

    internal class ServerMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
            if (urlInfo.Port > 0)
                Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
        }
    }

    internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

            if (string.IsNullOrEmpty(urlInfo.Username))
                throw new UsernameNotFoundException();
        }
    }

    internal class DatabaseMapper(string databaseName = EXCEPTION_DATABASE_NAME)
        : SingleSegmentTokenMapper(DATABASE_KEYWORD, databaseName, required: true);
}
