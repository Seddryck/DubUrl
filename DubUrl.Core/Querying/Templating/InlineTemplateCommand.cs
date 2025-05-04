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
    private IRenderingProxy Renderer { get; }

    public InlineTemplateCommand(string sql, IDialect dialect)
        : this(".st", sql, dialect)
    { }

    public InlineTemplateCommand(string extension, string sql, IDialect dialect)
    {
        var engine = new DidotEngine(extension);
        Renderer = engine.Prepare(sql, renderer: dialect.Renderer);
    }

    public string Render(IDictionary<string, object?> parameters)
        => Renderer.Render(parameters);
}
