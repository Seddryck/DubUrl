using DubUrl.Mapping;
using DubUrl.Provider.Teradata.Metadata;
using DubUrl.Provider.Teradata.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Teradata.Mapping;

[Mapper<TeradataDatabase, PositionalParametrizer>(
    "Teradata.Client"
)]
public class TeradataMapper : BaseMapper
{
    public TeradataMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new TeradataRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
