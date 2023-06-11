using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<PgsqlRenderer>()]
    internal class PgsqlDialect : BaseDialect
    {
        public PgsqlDialect(string[] aliases, IRenderer renderer)
            : base(aliases, renderer) { }
    }
}
