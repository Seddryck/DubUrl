using DubUrl.Mapping;
using DubUrl.Provider.Trino.Metadata;
using DubUrl.Provider.Trino.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Trino.Mapping;

[Mapper<TrinoDatabase, NamedParametrizer>(
    "NReco.PrestoAdo"
)]
[ConnectionConfiguration(Tls = "Trino")]
public class TrinoMapper : BaseMapper
{
    public TrinoMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new TrinoRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
