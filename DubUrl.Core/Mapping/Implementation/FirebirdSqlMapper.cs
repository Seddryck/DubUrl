using DubUrl.Mapping.Database;
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
    [Mapper<FirebirdSqlDatabase>("FirebirdSql.Data.FirebirdClient")]
    internal class FirebirdSqlMapper : BaseMapper
    {
        internal const string SERVER_KEYWORD = "DataSource";
        internal const string PORT_KEYWORD = "Port";
        internal const string DATABASE_KEYWORD = "Database";
        internal const string USERNAME_KEYWORD = "User";
        internal const string PASSWORD_KEYWORD = "Password";

        public FirebirdSqlMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb,
                  dialect,
                  new SpecificatorUnchecked(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new PortMapper(),
                    new AuthentificationMapper(),
                    new DatabaseMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (string.IsNullOrEmpty(urlInfo.Host) ||
                     StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "localhost") == 0 ||
                     StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ".") == 0 ||
                     urlInfo.Segments.Length == 0
                )
                    Specificator.Execute(SERVER_KEYWORD, "localhost");
                else
                {
                    Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
                }
            }
        }

        internal class PortMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Port > 0)
                    Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 0)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Host);
                else
                {
                    var path = new StringBuilder();
                    foreach (var segment in urlInfo.Segments)
                        if (!string.IsNullOrEmpty(segment))
                            path.Append(segment).Append(Path.DirectorySeparatorChar);
                    path.Remove(path.Length - 1, 1);
                    Specificator.Execute(DATABASE_KEYWORD, path.ToString());
                }
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
