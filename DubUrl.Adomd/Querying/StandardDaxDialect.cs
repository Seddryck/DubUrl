using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying;

[Renderer<DaxRenderer>()]
[ReturnCaster<DecimalConverter>()]
[ReturnCaster<DateTimeCaster<DateOnly>>()]
[ReturnCaster<DateTimeCaster<TimeOnly>>()]
[ParentLanguage<DaxLanguage>()]
public class StandardDaxDialect : BaseDialect
{
    internal StandardDaxDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters)
        : base(language, aliases, renderer, casters) { }
}
