using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<FirebirdSqlRenderer>()]
    [ReturnCaster<DateTimeCaster<DateOnly>>]
    [ReturnCaster<TimeSpanCaster<TimeOnly>>]
    public class FirebirdSqlDialect : BaseDialect
    {
        internal FirebirdSqlDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
            : base(aliases, renderer, casters) { }
    }
}
