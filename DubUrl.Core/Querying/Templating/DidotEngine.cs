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
using DubUrlRenderes = DubUrl.Querying.Dialects.Renderers;

namespace DubUrl.Querying.Templating;

internal class DidotEngine : ITemplatingProxy
{
    private ITemplateEngine Engine { get; }

    public DidotEngine(string extension)
    {
        var factory = new TemplateEngineFactory();
        factory.AddOrReplace(".st", Engine => Engine.UseStringTemplate(opt => opt.WithDollarDelimitedExpressions()));
        factory.AddOrReplace(".scriban", Engine => Engine.UseScriban());
        factory.AddOrReplace(".hdb", Engine => Engine.UseHandlebars());
        factory.AddOrReplace(".mustache", Engine => Engine.UseMorestachio());
        factory.AddOrReplace(".liquid", Engine => Engine.UseFluid());
        factory.Configure(config => config.WithoutWrapAsModel());
        Engine = factory.Create(extension);
    }

    public IRenderingProxy Prepare(string source)
        => new DidotRenderer(Engine.Prepare(source));

    public IRenderingProxy Prepare(string source, IDictionary<string, string>? subTemplates = null, IDictionary<string, IDictionary<string, object?>>? @dictionaries = null, DubUrlRenderes.IRenderer? renderer = null)
    {
        if (renderer is not null)
        {
            Engine.AddFormatter("value", (object? input) => renderer.Render(input, "value"));
            Engine.AddFormatter("identity", (object? input) => renderer.Render(input, "identity"));
        }
        if (subTemplates is not null)
            foreach (var subTemplate in subTemplates)
                Engine.AddFunction(subTemplate.Key, () => subTemplate.Value);
        if (@dictionaries is not null)
            foreach (var @dictionary in @dictionaries)
                Engine.AddMappings(@dictionary.Key, @dictionary.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? string.Empty));
        return new DidotRenderer(Engine.Prepare(source));
    }
}

internal class DidotRenderer : IRenderingProxy
{
    private Didot.Core.IRenderer Renderer { get; }

    public DidotRenderer(Didot.Core.IRenderer renderer)
        => Renderer = renderer;

    public string Render(IDictionary<string, object?> parameters)
        => Renderer.Render(parameters);
}
