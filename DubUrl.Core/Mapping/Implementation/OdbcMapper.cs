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
using System.Reflection;

namespace DubUrl.Mapping.Implementation;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
public class OdbcMapper : BaseMapper, IOdbcMapper
{
    public OdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this (csb, dialect, parametrizer, new DriverLocatorFactory()) { }

    public OdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, DriverLocatorFactory driverLocatorFactory)
        : base(new OdbcRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
