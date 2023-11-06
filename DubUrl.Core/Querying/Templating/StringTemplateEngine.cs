using Antlr4.StringTemplate;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    public class StringTemplateEngine
    {
        public string Render(string source, IDictionary<string, object?> parameters)
            => Render(source, new Dictionary<string, string>(), new Dictionary<string, IDictionary<string, object?>>(), parameters, null);

        public string Render(string source, IDictionary<string, object?> parameters, IRenderer? renderer)
            => Render(source, new Dictionary<string, string>(), new Dictionary<string, IDictionary<string, object?>>(), parameters, renderer);

        public string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, object?> parameters, IRenderer? renderer)
            => Render(source, subTemplates, new Dictionary<string, IDictionary<string, object?>>(), parameters, renderer);

        public string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, IDictionary<string, object?>> @dictionaries, IDictionary<string, object?> parameters, IRenderer? renderer)
        {
            foreach (var parameter in parameters)
                if (parameter.Value is null)
                    parameters[parameter.Key] = DBNull.Value;

            var template = new Template(source, '$', '$');
            if (renderer is not null)
                template.Group.RegisterRenderer(typeof(object), new SqlRendererWrapper(renderer));

            foreach (var subTemplate in subTemplates)
            {
                if(TryParseTemplate(subTemplate.Value, out var name, out var arguments, out var text))
                {
                    template.Group.DefineTemplate(subTemplate.Key, text, arguments);
                    if (name != subTemplate.Key)
                        template.Group.DefineTemplate(name, text, arguments);
                }
                else
                    template.Group.DefineTemplate(subTemplate.Key, subTemplate.Value);
            }
                

            foreach (var @dictionary in @dictionaries)
                template.Group.DefineDictionary(@dictionary.Key, @dictionary.Value);

            foreach (var parameter in parameters)
                template.Add(parameter.Key, parameter.Value);

            var actual = template.Render();
            return actual;
        }

        private static bool TryParseTemplate(string value, out string? name, out string[]? arguments, out string? template)
        {
            var end = value.IndexOf("::=");
            if (end < 0)
            {
                (name, arguments, template) = (null, null, value);
                return false;
            }

            var tokens = value[..end].Split('(');
            (name, arguments, template) = (tokens[0].Trim()
                , tokens[1].Trim()[..^1].Split(',').Select(x => x.Trim()).ToArray()
                , value[(end+3)..]);
            return true;
        }
    }
}
