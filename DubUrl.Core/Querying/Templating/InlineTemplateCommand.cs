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

    internal InlineTemplateCommand(DidotRenderer renderer, string template)
    {
        _renderer = renderer;
        Template = template;
    }
    
    public string Render(IDictionary<string, object?> parameters)
        => _renderer.Render(parameters);
}
