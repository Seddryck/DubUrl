using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation
{
    internal class Db2Rewriter : ConnectionStringRewriter
    {
        private const string EXCEPTION_DATABASE_NAME = "DB2";
        internal const string SERVER_KEYWORD = "Server";
        internal const string DATABASE_KEYWORD = "Database";
        internal const string USERNAME_KEYWORD = "User ID";
        internal const string PASSWORD_KEYWORD = "Password";

        public Db2Rewriter(DbConnectionStringBuilder csb)
            : base(
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
            public override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD,
                    $"{urlInfo.Host}{(urlInfo.Port > 0 ? $":{urlInfo.Port}" : string.Empty)}"
                );
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments == null || !urlInfo.Segments.Any())
                    throw new InvalidConnectionUrlMissingSegmentsException(EXCEPTION_DATABASE_NAME);
                else if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
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
            }
        }
    }
}
