using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<SnowflakeDialect>(
        "Snowflake"
        , new[] { "sf", "snowflake" }
        , "Snowflake.Data", 4
    )]
    internal class SnowflakeMapper : BaseMapper
    {
        internal const string SERVER_KEYWORD = "ACCOUNT";
        internal const string DATABASE_KEYWORD = "DB";
        internal const string SCHEMA_KEYWORD = "SCHEMA";
        internal const string USERNAME_KEYWORD = "USER";
        internal const string PASSWORD_KEYWORD = "PASSWORD";

        public SnowflakeMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb,
                  dialect,
                  new SpecificatorUnchecked(csb),
                  new BaseTokenMapper[] {
                    new AccountMapper(),
                    new DatabaseMapper(),
                    new SchemaMapper(),
                    new AuthentificationMapper(),
                  }
            )
        { }

        internal class AccountMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length >= 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
            }
        }

        internal class SchemaMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length >= 2)
                    Specificator.Execute(SCHEMA_KEYWORD, urlInfo.Segments.Skip(1).First());
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
            }
        }
    }
}
