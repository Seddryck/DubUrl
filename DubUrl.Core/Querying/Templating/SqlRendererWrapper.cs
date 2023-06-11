using Antlr4.StringTemplate;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    internal class SqlRendererWrapper : StringRenderer
    {
        protected IRenderer Renderer { get; }

        public SqlRendererWrapper(IRenderer renderer)
            => Renderer = renderer;

        public override string ToString(object? obj, string formatString, CultureInfo culture)
            => Renderer.Render(obj, formatString);
    }
}
