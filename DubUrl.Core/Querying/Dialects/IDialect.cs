using Antlr4.StringTemplate;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    public interface IDialect
    {
        public IRenderer Renderer { get; }
        public ICaster[] Casters { get; }

        string[] Aliases { get; }
    }
}
