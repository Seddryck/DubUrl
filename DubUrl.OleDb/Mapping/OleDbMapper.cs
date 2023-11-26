using DubUrl.Mapping.Connectivity;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.OleDb;
using DubUrl.Mapping;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Tokening;
using DubUrl.Rewriting;
using DubUrl.OleDb.Mapping;

namespace DubUrl.OleDb.Mapping;

[WrapperMapper<OleDbConnectivity, PositionalParametrizer>(
    "System.Data.OleDb"
)]
public class OleDbMapper : BaseMapper, IOleDbMapper
{
    public OleDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer) 
        : this(csb, dialect, parametrizer, new ProviderLocatorFactory()) { }
    public OleDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, ProviderLocatorFactory providerLocatorFactory)
        : base(new OleDbRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
