using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class DuckDBRenderer : AnsiRenderer
    {
        public DuckDBRenderer()
            : base(new ValueFormatter()
                  , new NullFormatter()
                  , new UnquotedIdentifierFormatter()) 
        { }
    }
}
