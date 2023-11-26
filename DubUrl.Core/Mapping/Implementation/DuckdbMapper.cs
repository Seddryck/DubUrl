using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation;

[MapperAttribute<DuckdbDatabase, PositionalParametrizer>(
    "DuckDB.NET.Data"
)]
internal class DuckdbMapper : BaseMapper
{
    public DuckdbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new DuckdbRewriter(csb),
              dialect,
              parametrizer
        )
    { }

    protected DuckdbMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        : base(rewriter,
              dialect,
              parametrizer
        )
    { }
}
