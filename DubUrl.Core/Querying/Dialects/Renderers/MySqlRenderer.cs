using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

internal class MySqlRenderer : AnsiRenderer
{
    public MySqlRenderer()
        : base(new ValueFormatter()
                    .With(new FunctionFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
              , new NullFormatter()
              , new BacktickIdentifierFormatter()) { }
}
