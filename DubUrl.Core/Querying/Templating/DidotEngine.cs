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
    private readonly string _extension;

    public DidotEngine(string extension)
        => _extension = extension;

    private ITemplateEngine CreateEngine()
    {
        var factory = new TemplateEngineFactory();
        factory.AddOrReplace(".st", Engine => Engine.UseStringTemplate(opt => opt.WithDollarDelimitedExpressions()));
        factory.AddOrReplace(".scriban", Engine => Engine.UseScriban());
        factory.AddOrReplace(".hdb", Engine => Engine.UseHandlebars());
        factory.AddOrReplace(".mustache", Engine => Engine.UseMorestachio());
        factory.AddOrReplace(".liquid", Engine => Engine.UseFluid());
        factory.Configure(config => config.WithoutWrapAsModel());
        return factory.Create(_extension);
    }

    public DidotRenderer Prepare(string source)
        => new (CreateEngine().Prepare(source));

    public DidotRenderer Prepare(string source, IDictionary<string, string>? subTemplates = null, IDictionary<string, IDictionary<string, object?>>? @dictionaries = null, DubUrlRenderes.IRenderer? renderer = null, Func<object?, string>? parameter = null)
    {
        // Formatters and mappings belong to the prepared template.
        var engine = CreateEngine();
        // StringTemplate catches formatter exceptions. Preserve parameter failures so
        // callers cannot accidentally execute partially rendered SQL.
        var failure = new AsyncLocal<Exception?>();
        engine.AddFormatter("parameter", (object? input) =>
        {
            try
            {
                if (input is AbsentTemplateInput absent) absent.Throw();
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
            string RenderChecked(object? input, string format)
            {
                if (input is AbsentTemplateInput absent)
                {
                    try { absent.Throw(); }
                    catch (ArgumentException exception) { failure.Value ??= exception; }
                    return string.Empty;
                }
                return renderer.Render(input, format);
            }
            engine.AddFormatter("value", (object? input) => RenderChecked(input, "value"));
            engine.AddFormatter("identity", (object? input) => RenderChecked(input, "identity"));
        }
        if (subTemplates is not null)
            foreach (var subTemplate in subTemplates)
                engine.AddFunction(subTemplate.Key, () => subTemplate.Value);
        if (@dictionaries is not null)
            foreach (var @dictionary in @dictionaries)
                engine.AddMappings(@dictionary.Key, @dictionary.Value.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? string.Empty));
        return new DidotRenderer(engine.Prepare(source), failure);
    }
}
