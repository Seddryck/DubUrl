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
public class DropIndexesIfExistsRenderer : RendererEngine
{
    public DropIndexesIfExistsRenderer(IDialect dialect)
            : this(CreateHelpers(dialect.Renderer))
    { }

    protected DropIndexesIfExistsRenderer(IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(DropIndexesIfExistsRenderer).Assembly, $"{typeof(DropIndexesIfExistsRenderer).Namespace}.Templates.DropIndexesIfExists.sql.hbs")
    {
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }
}
