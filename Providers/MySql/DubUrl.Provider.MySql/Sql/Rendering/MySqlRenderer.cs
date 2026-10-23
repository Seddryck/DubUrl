using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Sql.Rendering;

internal class MySqlRenderer : AnsiRenderer
{
    public MySqlRenderer()
        : base(new ValueFormatter()
                    .With(new FunctionFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
              , new NullFormatter()
              , new IdentifierBacktickFormatter()) { }

    protected MySqlRenderer(BaseValueFormatter value)
        : base(value, new NullFormatter(), new IdentifierBacktickFormatter()) { }
}
