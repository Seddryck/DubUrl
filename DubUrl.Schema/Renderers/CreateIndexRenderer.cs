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
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Functions;

namespace DubUrl.Schema.Renderers;
public class CreateIndexRenderer : RendererEngine
{
    public CreateIndexRenderer(IDialect dialect)
        : this(CreateHelpers(dialect.Renderer))
    { }
    
    protected CreateIndexRenderer(IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(CreateIndexRenderer).Assembly, $"{typeof(CreateIndexRenderer).Namespace}.Templates.CreateIndexes.sql.hbs")
    {
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }
}
