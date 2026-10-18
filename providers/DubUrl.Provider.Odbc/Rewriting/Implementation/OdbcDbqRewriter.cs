using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Locating.Options;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class OdbcDbqRewriter : ConnectionStringRewriter, IOdbcConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source) { Host = "localhost" };
        p.Segments.Add(p.Take("DBQ"));
        p.TakeCredentials("Uid", "Pwd");
        return p.ToUri(scheme);
    }

    protected internal const string SERVER_KEYWORD = "DBQ";
    protected internal const string USERNAME_KEYWORD = "Uid";
    protected internal const string PASSWORD_KEYWORD = "Pwd";
    protected internal const string DRIVER_KEYWORD = "Driver";

    public OdbcDbqRewriter(DbConnectionStringBuilder csb, string rootPath)
        : this(csb, rootPath, new BuiltInDriverLocatorFactory())
    { }

    public OdbcDbqRewriter(DbConnectionStringBuilder csb, string rootPath, DriverLocatorFactory driverLocatorFactory)
        : this(csb, rootPath, driverLocatorFactory, OdbcRewriter.DefaultLocatorOptions) { }

    public OdbcDbqRewriter(DbConnectionStringBuilder csb, string rootPath, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new StraightSpecificator(csb),
              [
                new DbqMapper(rootPath),
                new OdbcRewriter.AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory, locatorOptions),
                new OptionsMapper(),
              ]
        )
    { }

    internal class DbqMapper(string rootPath) : FilePathTokenMapper(SERVER_KEYWORD, rootPath, "ODBC DBQ");

    internal class DriverMapper : OdbcRewriter.DriverMapper
    {
        public DriverMapper(DriverLocatorFactory factory) : base(factory) { }
        public DriverMapper(DriverLocatorFactory factory, IEnumerable<Type> options) : base(factory, options) { }
        protected override string ConnectionName => "ODBC DBQ";
    }
}
