using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

internal class PgsqlRenderer : AnsiRenderer
{
    public PgsqlRenderer()
        : this(new ValueFormatter()) { }

    protected PgsqlRenderer(BaseValueFormatter value)
        : base(value, new NullFormatter(), new IdentifierQuotedFormatter()) { }
}
