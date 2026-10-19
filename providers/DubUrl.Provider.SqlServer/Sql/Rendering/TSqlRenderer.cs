using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.SqlServer.Sql.Rendering;

internal class TSqlRenderer : AnsiRenderer
{
    public TSqlRenderer()
        : base(new ValueFormatter()
                    .With(new IntervalAsTimeFormatter())
                    .With(new CastFormatter<bool>("BIT", new BooleanBitFormatter()))
                    .With(new CastFormatter<DateOnly>("DATE", new DateFormatter()))
                    .With(new CastFormatter<TimeOnly>("TIME", new TimeFormatter()))
                    .With(new CastFormatter<DateTime>("DATETIME", new TimestampFormatter()))
                    .With(new CastFormatter<DateTimeOffset>("DATETIMEOFFSET", new TimestampTimeZoneFormatter()))
                    .With(new CastFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
                , new NullFormatter()
                , new IdentifierSquareBracketFormatter()) { }
}
