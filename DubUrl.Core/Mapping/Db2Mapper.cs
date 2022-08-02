using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [Mapper(
        "IBM DB2"
        , new[] { "db2" }
        , "IBM.Data.DB2.Core", 3
    )]
    internal class Db2Mapper : BaseMapper
    {
        internal const string SERVER_KEYWORD = "Server";
        internal const string DATABASE_KEYWORD = "Database";
        internal const string USERNAME_KEYWORD = "User ID";
        internal const string PASSWORD_KEYWORD = "Password";

        public Db2Mapper(DbConnectionStringBuilder csb)
            : base(csb,
                  new Specificator(csb),
                  new BaseTokenMapper[] {
                    new ServerMapper(),
                    new DatabaseMapper(),
                    new AuthentificationMapper(),
                  }
            )
        { }

        internal class ServerMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD, 
                    $"{urlInfo.Host}{(urlInfo.Port > 0 ? $":{urlInfo.Port}" : string.Empty)}"
                );
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new ArgumentOutOfRangeException();
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
