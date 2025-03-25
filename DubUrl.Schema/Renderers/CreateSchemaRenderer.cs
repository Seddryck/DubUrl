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

namespace DubUrl.Schema.Renderers;
public class CreateSchemaRenderer : RendererEngine
{
    public CreateSchemaRenderer(IDialect dialect)
        : this(dialect.DbTypeMapper, CreateHelpers(dialect.Renderer))
    { }
    
    protected CreateSchemaRenderer(IDbTypeMapper typeMapper, IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(CreateSchemaRenderer).Assembly, $"{typeof(CreateSchemaRenderer).Namespace}.Templates.CreateTables.sql.hbs")
    {
        AddMappings("dbtype", typeMapper.ToDictionary());
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }
}
