using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<AnsiRenderer>()]
    internal class TeradataDialect : BaseDialect
    {
        public TeradataDialect(string[] aliases, IRenderer renderer)
            : base(aliases, renderer) { }
    }
}
