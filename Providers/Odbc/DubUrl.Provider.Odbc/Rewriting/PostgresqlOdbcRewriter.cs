using DubUrl.Conversion;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Rewriting.Tokening;
using System.Data.Common;

namespace DubUrl.Provider.Odbc.Rewriting;

public class PostgresqlOdbcRewriter : OdbcRewriter
{
    protected internal const string PORT_KEYWORD = "Port";

    public PostgresqlOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new BuiltInDriverLocatorFactory()) { }

    public PostgresqlOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory factory)
        : this(csb, factory, DefaultLocatorOptions) { }

    public PostgresqlOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory factory, IEnumerable<Type> locatorOptions)
        : base(csb,
            [
                new HostMapper(),
                new PortMapper(),
                new DatabaseMapper(),
                new AuthentificationMapper(),
                new DriverMapper(factory, locatorOptions),
                new OptionsMapper(),
            ]) { }

    public override ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var parts = new ConnectionStringParts(source);
        parts.TakeHost(SERVER_KEYWORD);
        parts.TakePort(PORT_KEYWORD);
        parts.Segments.Add(parts.Take(DATABASE_KEYWORD));
        parts.TakeCredentials(USERNAME_KEYWORD, PASSWORD_KEYWORD);
        return parts.ToUri(scheme);
    }

    protected internal new class HostMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri uri)
            => Specificator.Execute(SERVER_KEYWORD, uri.Host);
    }

    internal class PortMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri uri)
        {
            if (uri.Port > 0)
                Specificator.Execute(PORT_KEYWORD, uri.Port);
        }
    }
}
