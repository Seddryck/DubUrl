using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying.Mdx;

[Renderer<MdxRenderer>()]
[ReturnCaster<DecimalConverter>()]
[ReturnCaster<DateTimeCaster<DateOnly>>()]
[ReturnCaster<DateTimeCaster<TimeOnly>>()]
[ParentLanguage<MdxLanguage>()]
public class StandardMdxDialect : BaseDialect
{
    internal StandardMdxDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper)
        : base(language, aliases, renderer, casters, dbTypeMapper) { }
}
