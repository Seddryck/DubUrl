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

namespace DubUrl.Schema;
public class SchemaRenderer : RendererEngine
{
    public SchemaRenderer(IDbTypeMapper dbTypeMapper)
        : base(typeof(SchemaRenderer).Assembly, $"{typeof(SchemaRenderer).Namespace}.Templates.CreateSchema.sql.hbs")
    {
        AddMappings("dbtype", dbTypeMapper.ToDictionary());
        AddMappings("greetings", new Dictionary<string, object>() { { "fr", "Bonjour" }, { "en", "Hello" }, { "es", "Hola" } });
    }
}
