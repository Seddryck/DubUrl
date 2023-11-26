using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Renderer<PgsqlRenderer>()]
[ReturnCaster<DateTimeCaster<DateOnly>>]
[ReturnCaster<TimeSpanCaster<TimeOnly>>]
public class CockRoachDialect : BaseDialect
{
    internal CockRoachDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
        : base(aliases, renderer, casters) { }
}
