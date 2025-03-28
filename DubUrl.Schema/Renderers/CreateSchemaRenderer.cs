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
public class CreateSchemaRenderer : RendererEngine
{
    public CreateSchemaRenderer(IDialect dialect)
        : this(dialect.DbTypeMapper, dialect.SqlFunctionMapper, CreateHelpers(dialect.Renderer))
    { }
    
    protected CreateSchemaRenderer(IDbTypeMapper typeMapper, ISqlFunctionMapper sqlFunctionMapper, IDictionary<string, Func<object?, string>> helpers)
        : base(typeof(CreateSchemaRenderer).Assembly, $"{typeof(CreateSchemaRenderer).Namespace}.Templates.CreateTables.sql.hbs")
    {
        AddMappings("dbtype", typeMapper.ToDictionary());
        AddMappings("function", sqlFunctionMapper.ToDictionary());
        foreach (var helper in helpers)
            AddFormatter(helper.Key, helper.Value);
    }
}
