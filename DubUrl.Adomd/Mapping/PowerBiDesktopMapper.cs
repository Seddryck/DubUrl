using DubUrl.Adomd.Rewriting;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping;

[Mapper<PowerBiDesktopDatabase, NamedParametrizer>(
    "Microsoft.AnalysisServices.AdomdClient"
)]
public class PowerBiDesktopMapper : PowerBiPremiumMapper
{
    public PowerBiDesktopMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new PowerBiDesktopRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
