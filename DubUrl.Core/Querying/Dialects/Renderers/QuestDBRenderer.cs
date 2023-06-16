using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class QuestDBRenderer : PgsqlRenderer
    {
        public QuestDBRenderer()
            : base(new ValueFormatter()
                        .With(new FunctionFormatter<DateOnly>("TIMESTAMP", new DateFormatter())))
        { }
    }
}
