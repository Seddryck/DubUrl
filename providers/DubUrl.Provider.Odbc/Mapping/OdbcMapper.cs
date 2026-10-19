using DubUrl.Mapping;
using DubUrl.Provider.Odbc.Connectivity;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Mapping.Connectivity;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Locating.Options;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Tokening;
using DubUrl.Rewriting;
using System.Reflection;

namespace DubUrl.Provider.Odbc.Mapping;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
public class OdbcMapper : BaseMapper, IOdbcMapper
{
    public OdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this (csb, dialect, parametrizer, new BuiltInDriverLocatorFactory()) { }

    public OdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory)
        : this(csb, dialect, parametrizer, driverLocatorFactory, OdbcRewriter.DefaultLocatorOptions) { }

    public OdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new OdbcRewriter(csb, driverLocatorFactory, locatorOptions),
              dialect,
              parametrizer
        )
    { }
}
