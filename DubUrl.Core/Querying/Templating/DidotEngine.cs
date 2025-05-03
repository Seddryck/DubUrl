using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Didot.Core.TemplateEngines;
using Didot.Core;
using System.Reflection;

namespace DubUrl.Querying.Templating;

public class DidotEngine : IRenderingEngine
{
    private ITemplateEngine Engine { get; }

    public DidotEngine(string extension)
    {
        var factory = new TemplateEngineFactory();
        factory.AddOrReplace(".st", Engine => Engine.UseStringTemplate(opt => opt.WithDollarDelimitedExpressions()));
        factory.AddOrReplace(".scriban", Engine => Engine.UseScriban());
        factory.Configure(config => config.WithoutWrapAsModel());
        Engine = factory.Create(extension);
    }

    public string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, object?> parameters, IRenderer? renderer)
        => Render(source, subTemplates, new Dictionary<string, IDictionary<string, object?>>(), parameters, renderer);

    public string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, IDictionary<string, object?>> @dictionaries, IDictionary<string, object?> parameters, IRenderer? renderer)
    {
        if (renderer is not null)
        {
            Engine.AddFormatter("value", (object? input) => renderer.Render(input, "value"));
            Engine.AddFormatter("identity", (object? input) => renderer.Render(input, "identity"));
        }

        foreach (var subTemplate in subTemplates)
            Engine.AddFunction(subTemplate.Key, () => subTemplate.Value);

        foreach (var @dictionary in @dictionaries)
            Engine.AddMappings(@dictionary.Key, @dictionary.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? string.Empty));

        foreach (var parameter in parameters)
            if (parameter.Value is null)
                parameters[parameter.Key] = DBNull.Value;

        var actual = Engine.Render(source, parameters);
        return actual;
    }
}
