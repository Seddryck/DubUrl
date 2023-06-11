using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class SqliteRenderer : AnsiRenderer
    {
        public SqliteRenderer()
            : base(new ValueFormatter()
                        .With(new BooleanLowerFormatter())
                        .With(new FunctionFormatter<DateOnly>("date", new DateFormatter()))
                        .With(new FunctionFormatter<TimeOnly>("time", new TimeFormatter()))
                        .With(new FunctionFormatter<DateTime>("datetime", new TimestampFormatter()))
                  , new NullFormatter()
                  , new UnquotedIdentifierFormatter())
        { }
    }
}
