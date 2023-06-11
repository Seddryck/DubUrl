using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    [Renderer<MySqlRenderer>()]
    internal class MySqlDialect : BaseDialect
    {
        public MySqlDialect(string[] aliases, IRenderer renderer)
            : base(aliases, renderer) { }
    }
}
