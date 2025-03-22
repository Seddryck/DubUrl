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
using DubUrl.Schema.Renderers;

namespace DubUrl.Schema;
public class SchemaRenderer
{
    private RendererEngine[] Renderers { get; } = [];

    public SchemaRenderer(IDialect dialect, SchemaCreationOptions options = SchemaCreationOptions.None)
        : this(dialect.DbTypeMapper, options)
    { }

    public SchemaRenderer(IDbTypeMapper typeMapper, SchemaCreationOptions options = SchemaCreationOptions.None)
    {
        var renderers = new List<RendererEngine>();
        if (options == SchemaCreationOptions.DropIfExists)
            renderers.Add(new DropTablesIfExistsRenderer(typeMapper));

        renderers.Add(new CreateSchemaRenderer(typeMapper));
        Renderers = [.. renderers];
    }

    public string Render(object schema)
    {
        var script = new StringBuilder();
        foreach (var renderer in Renderers)
            script.Append(renderer.Render(schema));
        return script.ToString();
    }
}
