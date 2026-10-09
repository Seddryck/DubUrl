using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System.Data.Common;

namespace DubUrl.Rewriting.Implementation;

public class PostgresqlOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
{
    protected internal const string PORT_KEYWORD = "Port";

    public PostgresqlOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new DriverLocatorFactory()) { }

    public PostgresqlOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : base(csb,
              [
                  new HostMapper(),
                  new PortMapper(),
                  new DatabaseMapper(),
                  new AuthentificationMapper(),
                  new DriverMapper(driverLocatorFactory),
                  new OptionsMapper(),
              ])
    { }

    protected internal new class HostMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
            => Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
    }

    protected internal class PortMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Port > 0)
                Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
        }
    }
}
