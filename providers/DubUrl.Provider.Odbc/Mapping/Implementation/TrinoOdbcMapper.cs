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
using DubUrl.Rewriting.Implementation;

namespace DubUrl.Mapping.Implementation;

[WrapperMapper<OdbcConnectivity, NamedParametrizer>(
    "System.Data.Odbc"
)]
public class TrinoOdbcMapper : BaseMapper, IOdbcMapper
{
    public TrinoOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this (csb, dialect, parametrizer, new BuiltInDriverLocatorFactory()) { }

    public TrinoOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory)
        : this(csb, dialect, parametrizer, driverLocatorFactory, OdbcRewriter.DefaultLocatorOptions) { }

    public TrinoOdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new TrinoOdbcRewriter(csb, driverLocatorFactory, locatorOptions),
              dialect,
              parametrizer
        )
    { }
}
