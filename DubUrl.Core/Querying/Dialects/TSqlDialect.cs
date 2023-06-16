using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<TSqlRenderer>()]
    [ReturnCaster<BooleanConverter>]
    [ReturnCaster<DecimalConverter>]
    [ReturnCaster<DateTimeCaster<DateOnly>>]
    [ReturnCaster<TimeSpanCaster<TimeOnly>>]
    public class TSqlDialect : BaseDialect
    {
        public TSqlDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
            : base(aliases, renderer, casters) { }
    }
}
