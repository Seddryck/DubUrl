using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Rewriting;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.Options;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Rewriting;

public class DuckdbOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
{
    public DuckdbOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new BuiltInDriverLocatorFactory()) { }
    public DuckdbOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : this(csb, driverLocatorFactory, DefaultLocatorOptions) { }
    public DuckdbOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(csb,
              [
                new HostMapper(),
                new DatabaseMapper(),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory, locatorOptions),
                new OptionsMapper(),
              ]
        )
    { }

    protected internal new class HostMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Host))
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
        }
    }
}
