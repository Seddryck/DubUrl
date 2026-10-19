using DubUrl.Provider.Snowflake.Conversion;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Snowflake.Rewriting;

internal class SnowflakeRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Provider.Snowflake.Conversion.SnowflakeConnectionStringReverser.Convert(scheme, source);

    internal const string SERVER_KEYWORD = "ACCOUNT";
    internal const string DATABASE_KEYWORD = "DB";
    internal const string SCHEMA_KEYWORD = "SCHEMA";
    internal const string USERNAME_KEYWORD = "USER";
    internal const string PASSWORD_KEYWORD = "PASSWORD";

    public SnowflakeRewriter(DbConnectionStringBuilder csb)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new AccountMapper(),
                new DatabaseMapper(),
                new SchemaMapper(),
                new AuthentificationMapper(),
              ]
        )
    { }

    internal class AccountMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
        }
    }

    internal class DatabaseMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (urlInfo.Segments.Count >= 1)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
        }
    }

    internal class SchemaMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (urlInfo.Segments.Count >= 2)
                Specificator.Execute(SCHEMA_KEYWORD, urlInfo.Segments.Skip(1).First());
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
        }
    }
}
