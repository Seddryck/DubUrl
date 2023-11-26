using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.Options;
using DubUrl.Mapping;
using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DubUrl.Rewriting.Implementation;

public class DrillOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
{
    private const string EXCEPTION_DATABASE_NAME = "ODBC for Apache Drill";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const string SCHEMA_KEYWORD = "Schema";
    protected internal const string AUTHENTICATION_KEYWORD = "AuthenticationType";
    protected internal const string CONNECTION_KEYWORD = "ConnectionType";
    protected internal const string ZKQUORUM_KEYWORD = "ZKQuorum";
    protected internal const string ZKCLUSTERID_KEYWORD = "ZKClusterID";

    public DrillOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new DriverLocatorFactory()) { }
    public DrillOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : base(csb,
              new BaseTokenMapper[] {
                new HostMapper(),
                new PortMapper(),
                new SchemaMapper(),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory),
                new OptionsMapper(),
              }
        )
    { }

    protected internal new class HostMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Host.Contains(','))
            {
                var quorum = new StringBuilder();
                foreach (var server in urlInfo.Host.Split(','))
                {
                    quorum.Append(server.Trim());
                    if (!server.Contains(':'))
                        quorum.Append(":31010");
                    quorum.Append(", ");
                }
                quorum.Remove(quorum.Length - 2, 2);
                Specificator.Execute(ZKQUORUM_KEYWORD, quorum.ToString());
                if (!urlInfo.Options.ContainsKey(ZKCLUSTERID_KEYWORD))
                    Specificator.Execute(ZKCLUSTERID_KEYWORD, "drillbits1");
                if (!urlInfo.Options.ContainsKey(CONNECTION_KEYWORD))
                    Specificator.Execute(CONNECTION_KEYWORD, "ZooKeeper");
            }
            else
            {
                Specificator.Execute(SERVER_KEYWORD, !string.IsNullOrEmpty(urlInfo.Host) ? urlInfo.Host : "localhost");
                if (!urlInfo.Options.ContainsKey(CONNECTION_KEYWORD))
                    Specificator.Execute(CONNECTION_KEYWORD, "Direct");
            }
                
        }
    }

    protected internal class PortMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
            => Specificator.Execute(PORT_KEYWORD, urlInfo.Port > 0 ? urlInfo.Port : 31010);
    }
    protected internal class SchemaMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length == 1 || (urlInfo.Segments.Length == 2 && urlInfo.Segments.First() == "DRILL"))
                Specificator.Execute(SCHEMA_KEYWORD, urlInfo.Segments.Last());
            else if (urlInfo.Segments.Length > 1)
                throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
        }
    }

    protected internal new class AuthentificationMapper : OdbcRewriter.AuthentificationMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            base.Execute(urlInfo);
            if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(AUTHENTICATION_KEYWORD, "No Authentication");
            else
                Specificator.Execute(AUTHENTICATION_KEYWORD, "Plain");
        }
    }
}
