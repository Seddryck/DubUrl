using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class TrinoOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
{
    protected internal const string HOST_KEYWORD = "Host";
    protected internal const string HOST_DEFAULT_VALUE = "localhost";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const int PORT_DEFAULT_VALUE = 8080;
    protected internal const string CATALOG_KEYWORD = "Catalog";
    protected internal const string SCHEMA_KEYWORD = "Schema";

    public TrinoOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new DriverLocatorFactory()) { }
    public TrinoOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : base(csb,
              [
                new HostMapper(),
                new PortMapper(),
                new CatalogMapper(),
                new SchemaMapper(),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory),
                new OptionsMapper(),
              ]
        )
    { }

    internal new class HostMapper : BaseTokenMapper
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
}
