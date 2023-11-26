using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DubUrl.Mapping.Implementation;

[WrapperMapper<OdbcConnectivity, PositionalParametrizer>(
    "System.Data.Odbc"
)]
internal class OdbcDbqMapper : BaseMapper, IOdbcMapper
{ 
    public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new OdbcDbqRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
