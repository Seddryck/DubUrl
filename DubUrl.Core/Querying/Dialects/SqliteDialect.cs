using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Renderer<SqliteRenderer>()]
[ReturnCaster<BooleanConverter>]
[ReturnCaster<DecimalConverter>]
[ReturnCaster<Parser<DateOnly>>]
[ReturnCaster<Parser<TimeOnly>>]
[ReturnCaster<Parser<DateTime>>]
[ReturnCaster<Parser<TimeSpan>>]
public class SqliteDialect : BaseDialect
{
    internal SqliteDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
        : base(aliases, renderer, casters) { }
}
