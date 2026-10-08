using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System.Data.Common;

namespace DubUrl.Mapping.Implementation;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
public class PostgresqlOdbcMapper : BaseMapper, IOdbcMapper
{
    public PostgresqlOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this(csb, dialect, parametrizer, new DriverLocatorFactory()) { }

    public PostgresqlOdbcMapper(
        DbConnectionStringBuilder csb,
        IDialect dialect,
        IParametrizer parametrizer,
        DriverLocatorFactory driverLocatorFactory)
        : base(new PostgresqlOdbcRewriter(csb, driverLocatorFactory), dialect, parametrizer)
    { }
}
