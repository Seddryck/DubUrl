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
        public string Render(string source, IDictionary<string, object?> parameters)
            => Render(source, new Dictionary<string, string>(), parameters, null);

        public string Render(string source, IDictionary<string, object?> parameters, IRenderer? renderer)
            => Render(source, new Dictionary<string, string>(), parameters, renderer);

        public string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, object?> parameters, IRenderer? renderer)
        {
            foreach (var parameter in parameters)
                if (parameter.Value is null)
                    parameters[parameter.Key] = DBNull.Value;

            var template = new Template(source, '$', '$');
            if (renderer is not null)
                template.Group.RegisterRenderer(typeof(object), new SqlRendererWrapper(renderer));

            foreach (var subTemplate in subTemplates)
                template.Group.DefineTemplate(subTemplate.Key, subTemplate.Value);

            foreach (var parameter in parameters)
                template.Add(parameter.Key, parameter.Value);

            var actual = template.Render();
            return actual;
        }
    }
}