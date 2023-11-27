using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Renderer<DuckDBRenderer>()]
[ReturnCaster<Converter<DateOnly>>]
[ReturnCaster<Converter<TimeOnly>>]
[ReturnCaster<Converter<DateTime>>]
[ReturnCaster<Converter<TimeSpan>>]
[ParentLanguage<SqlLanguage>]
public class DuckdbDialect : BaseDialect
{
    internal DuckdbDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters)
        : base(language, aliases, renderer, casters) { }
}
