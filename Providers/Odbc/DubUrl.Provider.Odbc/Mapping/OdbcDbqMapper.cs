using DubUrl.Mapping;
using DubUrl.Provider.Odbc.Connectivity;
using DubUrl.Provider.Odbc.Drivers.Discovery;
using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Odbc.Mapping;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
public class OdbcDbqMapper : BaseMapper, IOdbcMapper, IFileBasedMapper
{ 
    public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath, IEnumerable<Type> locatorOptions)
        : this(csb, dialect, parametrizer, rootPath, new BuiltInDriverLocatorFactory(), locatorOptions) { }

    public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath)
        : this(csb, dialect, parametrizer, rootPath, new BuiltInDriverLocatorFactory(), OdbcRewriter.DefaultLocatorOptions) { }

    public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new OdbcDbqRewriter(csb, rootPath, driverLocatorFactory, locatorOptions),
              dialect,
              parametrizer
        )
    { }
}
