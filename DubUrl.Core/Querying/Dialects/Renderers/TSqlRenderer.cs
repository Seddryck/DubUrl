using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class TSqlRenderer : AnsiRenderer
    {
        public TSqlRenderer()
            : base(new ValueFormatter()
                        .With(new IntervalTimeFormatter())
                        .With(new CastFormatter<bool>("BIT", new BooleanBitFormatter()))
                        .With(new CastFormatter<DateOnly>("DATE", new DateFormatter()))
                        .With(new CastFormatter<TimeOnly>("TIME", new TimeFormatter()))
                        .With(new CastFormatter<DateTime>("DATETIME", new TimestampFormatter()))
                        .With(new CastFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
                    , new NullFormatter()
                    , new SquareBracketIdentifierFormatter()) { }
    }
}
