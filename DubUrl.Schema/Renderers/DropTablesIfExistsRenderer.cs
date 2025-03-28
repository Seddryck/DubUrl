using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Didot.Core.TemplateEngines;
using Didot.Core;
using System.Reflection;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Templating;
using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;
public class DropTablesIfExistsRenderer : RendererEngine
{
    public DropTablesIfExistsRenderer(IDialect dialect)
            : this(CreateHelpers(dialect.Renderer))
    { }

    protected DropTablesIfExistsRenderer(IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(DropTablesIfExistsRenderer).Assembly, $"{typeof(DropTablesIfExistsRenderer).Namespace}.Templates.DropTablesIfExists.sql.hbs")
    {
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }
}
