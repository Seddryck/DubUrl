using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class TrinoRenderer : AnsiRenderer
    {
        public TrinoRenderer()
            : base(new ValueFormatter()
                        .With(new IntervalTrinoFormatter())
                    , new NullFormatter()
                    , new QuotedIdentifierFormatter()) { }
    }
}
