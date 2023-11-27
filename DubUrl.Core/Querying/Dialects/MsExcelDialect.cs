using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Renderer<AnsiRenderer>()]
[ParentLanguage<SqlLanguage>]
public class MsExcelDialect : BaseDialect
{
    internal MsExcelDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters)
        : base(language, aliases, renderer, casters) { }
}
