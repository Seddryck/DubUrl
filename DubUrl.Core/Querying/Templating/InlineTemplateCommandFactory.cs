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

public class InlineTemplateCommandFactory
{
    private readonly IDialect _dialect;
    private readonly DidotEngine _engine;

    public InlineTemplateCommandFactory(IDialect dialect)
    {
        _dialect = dialect;
        _engine = new DidotEngine(".st");
    }

    public InlineTemplateCommand Create(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Template cannot be null or whitespace", nameof(template));
        var renderer = _engine.Prepare(template, renderer: _dialect.Renderer);
        return new InlineTemplateCommand(renderer, template);
    }
}
