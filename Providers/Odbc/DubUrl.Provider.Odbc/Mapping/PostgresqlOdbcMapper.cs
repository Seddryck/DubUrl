using DubUrl.Mapping;
using DubUrl.Provider.Odbc.Connectivity;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using System.Data.Common;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Provider.Odbc.Mapping;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
public class PostgresqlOdbcMapper : BaseMapper, IOdbcMapper
{
    public PostgresqlOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, IEnumerable<Type> locatorOptions)
        : this(csb, dialect, parametrizer, new BuiltInDriverLocatorFactory(), locatorOptions) { }

    public PostgresqlOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this(csb, dialect, parametrizer, new BuiltInDriverLocatorFactory()) { }

    public PostgresqlOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory)
        : this(csb, dialect, parametrizer, driverLocatorFactory, OdbcRewriter.DefaultLocatorOptions) { }

    public PostgresqlOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new PostgresqlOdbcRewriter(csb, driverLocatorFactory, locatorOptions),
              dialect,
              parametrizer
        )
    { }
}
