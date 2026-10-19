using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.FirebirdSql.Sql.Rendering;

internal class FirebirdSqlRenderer : AnsiRenderer
{
    public FirebirdSqlRenderer()
        : base(new ValueFormatter()
                    .With(new PrefixFormatter<TimeSpan>("TIME", new IntervalAsTimeFormatter()))
              , new NullFormatter()
              , new IdentifierQuotedFormatter())
    { }
}
