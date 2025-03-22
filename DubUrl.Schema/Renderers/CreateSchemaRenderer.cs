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
public class CreateSchemaRenderer : RendererEngine
{
    public CreateSchemaRenderer(IDialect dialect)
        : this(dialect.DbTypeMapper)
    { }

    public CreateSchemaRenderer(IDbTypeMapper typeMapper)
        : base(typeof(CreateSchemaRenderer).Assembly, $"{typeof(CreateSchemaRenderer).Namespace}.Templates.CreateTables.sql.hbs")
    {
        AddMappings("dbtype", typeMapper.ToDictionary());
    }
}
