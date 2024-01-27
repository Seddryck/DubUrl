using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

internal class SingleStoreRenderer : MySqlRenderer
{
    public SingleStoreRenderer()
        : base(new ValueFormatter()
                    .With(new FunctionFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
                    .With(new FunctionFormatter<DateOnly>("DATE", new DateFormatter()))
                    .With(new FunctionFormatter<TimeOnly>("TIME", new TimeFormatter()))
                    .With(new FunctionFormatter<DateTime>("TIMESTAMP", new TimestampFormatter()))
              ) { }
}
