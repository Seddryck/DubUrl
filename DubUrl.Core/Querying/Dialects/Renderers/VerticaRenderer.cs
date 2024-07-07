using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

internal class VerticaRenderer : AnsiRenderer
{
    public VerticaRenderer()
        : base(new ValueFormatter()
                    .With(new PrefixFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
                    .With(new PrefixFormatter<DateOnly>("DATE", new DateFormatter()))
                    .With(new PrefixFormatter<TimeOnly>("TIME", new TimeFormatter()))
                    .With(new PrefixFormatter<DateTime>("TIMESTAMP", new TimestampFormatter()))

              , new NullFormatter()
              , new IdentifierQuotedFormatter()
        ) { }
}
