using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class FirebirdSqlRewriter : ConnectionStringRewriter
{
    internal const string SERVER_KEYWORD = "DataSource";
    internal const string PORT_KEYWORD = "Port";
    internal const string DATABASE_KEYWORD = "Database";
    internal const string USERNAME_KEYWORD = "User";
    internal const string PASSWORD_KEYWORD = "Password";

    public FirebirdSqlRewriter(DbConnectionStringBuilder csb, string rootPath)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new DataSourceMapper(),
                new PortMapper(),
                new AuthentificationMapper(),
                new DatabaseMapper(rootPath),
                new OptionsMapper(),
              ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
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
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Port > 0)
                Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
        }
    }

    internal class DatabaseMapper : BaseTokenMapper
    {
        private readonly string RootPath;

        public DatabaseMapper(string rootPath)
        {
            if (!rootPath.EndsWith(Path.DirectorySeparatorChar) && !string.IsNullOrEmpty(rootPath))
                rootPath += Path.DirectorySeparatorChar.ToString();
            RootPath = rootPath;
        }

        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length == 0)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Host);
            else
            {
                var path = new StringBuilder();
                if (!urlInfo.Segments.First().Contains(':'))
                    path.Append(RootPath);
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
        public override void Execute(UrlInfo urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
        }
    }
}
