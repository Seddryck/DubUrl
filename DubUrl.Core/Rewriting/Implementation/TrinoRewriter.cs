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
    internal class TrinoRewriter : ConnectionStringRewriter
    {
        protected internal const string HOST_KEYWORD = "Host";
        protected internal const string HOST_DEFAULT_VALUE = "localhost";
        protected internal const string PORT_KEYWORD = "Port";
        protected internal const int PORT_DEFAULT_VALUE = 8080;
        protected internal const string CATALOG_KEYWORD = "Catalog";
        protected internal const string SCHEMA_KEYWORD = "Schema";
        protected internal const string USERNAME_KEYWORD = "User";
        protected internal const string PASSWORD_KEYWORD = "Password";
        protected internal const string HEADERS_KEYWORD = "TrinoHeaders";
        protected internal const int HEADERS_DEFAULT_VALUE = 1;

        public TrinoRewriter(DbConnectionStringBuilder csb)
            : base(new UniqueAssignmentSpecificator(csb),
                  new BaseTokenMapper[] {
                    new HostMapper(),
                    new PortMapper(),
                    new CatalogMapper(),
                    new SchemaMapper(),
                    new AuthentificationMapper(),
                    new OptionsMapper(),
                  }
            )
        { }

        internal class HostMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
                => Specificator.Execute(HOST_KEYWORD, string.IsNullOrEmpty(urlInfo.Host) ? "localhost" : urlInfo.Host);
        }

        internal class PortMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
                => Specificator.Execute(PORT_KEYWORD, urlInfo.Port==0  ? PORT_DEFAULT_VALUE : urlInfo.Port);
        }

        internal class CatalogMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length >= 1)
                    Specificator.Execute(CATALOG_KEYWORD, urlInfo.Segments[0]);
            }
        }

        internal class SchemaMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length >= 2)
                    Specificator.Execute(SCHEMA_KEYWORD, urlInfo.Segments[1]);
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

        internal class OptionsMapper : Tokening.OptionsMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (!urlInfo.Options.ContainsKey(HEADERS_KEYWORD))
                    Specificator.Execute(HEADERS_KEYWORD, HEADERS_DEFAULT_VALUE);
                base.Execute(urlInfo);
            }
        }
    }
}
