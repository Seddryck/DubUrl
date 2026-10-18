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
    public override ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source);
        p.TakeHost("Host");
        p.TakePort("Port");
        p.TakeCredentials("Uid", "Pwd");
        p.Segments.Add(p.Take("Catalog"));
        var schema = p.Take("Schema");
        if (schema.Length > 0) p.Segments.Add(schema);
        return p.ToUri(scheme);
    }

    protected internal const string HOST_KEYWORD = "Host";
    protected internal const string HOST_DEFAULT_VALUE = "localhost";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const int PORT_DEFAULT_VALUE = 8080;
    protected internal const string CATALOG_KEYWORD = "Catalog";
    protected internal const string SCHEMA_KEYWORD = "Schema";

    public TrinoOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new BuiltInDriverLocatorFactory()) { }
    public TrinoOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : this(csb, driverLocatorFactory, DefaultLocatorOptions) { }
    public TrinoOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(csb,
              [
                new HostTokenMapper(HOST_KEYWORD, HOST_DEFAULT_VALUE),
                new PortTokenMapper(PORT_KEYWORD, PORT_DEFAULT_VALUE),
                new SegmentTokenMapper(CATALOG_KEYWORD, 0),
                new SegmentTokenMapper(SCHEMA_KEYWORD, 1),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory, locatorOptions),
                new OptionsMapper(),
              ]
        )
    { }
}
