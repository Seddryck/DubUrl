using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Access.Sql.Rendering;

internal class MsAccessRenderer : AnsiRenderer
{
    public MsAccessRenderer()
        : base(new ValueFormatter()
                    .With(new DateCrossSurroundingFormatter())
              , new NullFormatter()
              , new IdentifierSquareBracketFormatter()) { }

    protected MsAccessRenderer(BaseValueFormatter value)
        : base(value, new NullFormatter(), new IdentifierBacktickFormatter()) { }
}
