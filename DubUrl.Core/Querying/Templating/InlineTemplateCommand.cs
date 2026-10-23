using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating;

public class InlineTemplateCommand
{
    private readonly DidotRenderer _renderer;
    public string Template { get; }
    private readonly IDialect? _dialect;
    private readonly IParametrizer? _parametrizer;

    internal InlineTemplateCommand(DidotRenderer renderer, string template, IDialect? dialect = null, IParametrizer? parametrizer = null)
    {
        _renderer = renderer;
        Template = template;
        _dialect = dialect;
        _parametrizer = parametrizer;
    }
    
    public string Render(IDictionary<string, object?> parameters)
        => _renderer.Render(parameters);

    /// <summary>Renders SQL and ordered bindings using this template's connector.</summary>
    public RenderedTemplateCommand RenderCommand(IDictionary<string, object?> parameters)
        => RenderCommand(parameters, _parametrizer ?? throw new InvalidOperationException("Supply a connector parametrizer to render bound parameters."));

    public RenderedTemplateCommand RenderCommand(IDictionary<string, object?> parameters, IParametrizer parametrizer)
        => RenderBound(Template, parameters, _dialect?.Renderer, parametrizer);

    internal static RenderedTemplateCommand RenderBound(string source, IDictionary<string, object?> parameters,
        Dialects.Renderers.IRenderer? renderer, IParametrizer parametrizer,
        IDictionary<string, string>? subTemplates = null, IDictionary<string, IDictionary<string, object?>>? dictionaries = null)
    {
        var bindings = new List<TemplateBinding>();
        var prepared = new DidotEngine(".st").Prepare(source, subTemplates, dictionaries, renderer, input =>
        {
            var position = bindings.Count + 1;
            var name = "p" + position.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var placeholder = parametrizer.GetPlaceholder(position);
            bindings.Add(new TemplateBinding(name, input));
            return placeholder;
        });
        var values = parameters.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value));
        return new RenderedTemplateCommand(prepared.Render(values), bindings, parametrizer);
    }

    private static object? NormalizeValue(object? value) => value switch
    {
        null => DBNull.Value,
        // StringTemplate otherwise treats binary data as a sequence of parameters.
        byte[] bytes => new TemplateParameter(bytes, DbType.Binary),
        IDictionary<string, object?> dictionary => dictionary.ToDictionary(pair => pair.Key, pair => NormalizeValue(pair.Value)),
        System.Collections.IEnumerable sequence when value is not string => sequence.Cast<object?>().Select(NormalizeValue).ToArray(),
        _ => value
    };
}
