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
            : base(new ValueFormatter().With(new IntervalTimeFormatter())
                    , new NullFormatter()
                    , new SquareBracketIdentifierFormatter()) { }
    }
}
