using Antlr4.StringTemplate;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    internal class StringTemplateEngine
    {
        public string Render(string source, IDictionary<string, object?> parameters, IRenderer renderer)
        {
            var template = new Template(source, '$', '$');
            template.Group.RegisterRenderer(typeof(object), new SqlRendererWrapper(renderer));

            foreach (var parameter in parameters)
                template.Add(parameter.Key, parameter.Value);

            var actual = template.Render();
            return actual;
        }
    }
}
