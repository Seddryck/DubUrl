using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Sql.Rendering;

internal class QuestDBRenderer : PgsqlRenderer
{
    public QuestDBRenderer()
        : base(new ValueFormatter()
                    .With(new FunctionFormatter<DateOnly>("TIMESTAMP", new DateFormatter())))
    { }
}
