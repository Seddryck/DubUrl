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

internal class DidotEngine
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

    public DidotRenderer Prepare(string source)
        => new (Engine.Prepare(source));

    public DidotRenderer Prepare(string source, IDictionary<string, string>? subTemplates = null, IDictionary<string, IDictionary<string, object?>>? @dictionaries = null, DubUrlRenderes.IRenderer? renderer = null, Func<object?, string>? parameter = null)
    {
        // StringTemplate catches formatter exceptions. Preserve parameter failures so
        // callers cannot accidentally execute partially rendered SQL.
        var failure = new AsyncLocal<Exception?>();
        Engine.AddFormatter("parameter", (object? input) =>
        {
            try
            {
                return (parameter ?? throw new InvalidOperationException("Use RenderCommand to render bound parameters."))(input);
            }
            catch (Exception exception)
            {
                failure.Value ??= exception;
                return string.Empty;
            }
        });
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
        return new DidotRenderer(Engine.Prepare(source), failure);
    }
}
