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
using Antlr4.StringTemplate;
using DubUrl.Mapping;

namespace DubUrl.Querying.Templating;

internal class InlineTemplateCommand : InlineCommand
{
    public IDictionary<string, object?> Parameters { get; }

    public InlineTemplateCommand(string sql, IDictionary<string, object?> parameters, IQueryLogger queryLogger)
        : base(sql, queryLogger) { Parameters = parameters; }

    protected override string Render(IDialect dialect, IConnectivity connectivity)
        =>  new StringTemplateEngine().Render(Text, new Dictionary<string, string>(), Parameters, dialect.Renderer);
}
