using DubUrl.Provider.Trino.Sql.Formatting;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Trino.Sql.Rendering;

internal class TrinoRenderer : AnsiRenderer
{
    public TrinoRenderer()
        : base(new ValueFormatter()
                    .With(new IntervalTrinoFormatter())
                , new NullFormatter()
                , new IdentifierQuotedFormatter()) { }
}
