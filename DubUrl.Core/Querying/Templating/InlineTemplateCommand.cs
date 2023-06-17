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

namespace DubUrl.Querying.Templating
{
    internal class InlineTemplateCommand : InlineCommand
    {
        public IDictionary<string, object?> Parameters { get; }

        public InlineTemplateCommand(string sql, IDictionary<string, object?> parameters)
            : base(sql) { Parameters = parameters; }

        public override string Read(IDialect dialect, string? connectivity)
            =>  new StringTemplateEngine().Render(base.Read(dialect, connectivity), Parameters, dialect.Renderer);
    }
}
