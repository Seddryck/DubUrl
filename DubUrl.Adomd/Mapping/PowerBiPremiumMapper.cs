using DubUrl.Adomd.Rewriting;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping;

[Mapper<PowerBiPremiumDatabase, NamedParametrizer>(
    "Microsoft.AnalysisServices.AdomdClient"
)]
public class PowerBiPremiumMapper : BaseMapper
{
    public PowerBiPremiumMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : this(new PowerBiPremiumRewriter(csb),
              dialect,
              parametrizer
        )
    { }

    protected PowerBiPremiumMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        : base(rewriter,
              dialect,
              parametrizer
        )
    { }
}
