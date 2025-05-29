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
public class SchemaScriptRenderer
{
    private RendererEngine[] Templates { get; } = [];

    public SchemaScriptRenderer(IDialect dialect, SchemaCreationOptions options = SchemaCreationOptions.None)
    {
        var templates = new List<RendererEngine>();
        if (options == SchemaCreationOptions.DropIfExists)
        {
            templates.Add(new DropTablesIfExistsRenderer(dialect));
            templates.Add(new DropIndexesIfExistsRenderer(dialect));
        }
            
        templates.Add(new CreateSchemaRenderer(dialect));
        templates.Add(new CreateIndexRenderer(dialect));
        Templates = [.. templates];
    }

    public virtual string Render(Schema schema)
    {
        var tables = new List<TableViewModel>();
        foreach (var table in schema.Tables)
        {
            var tableRenderer = new TableViewModel(table.Value);
            tables.Add(tableRenderer);
        }
        var model = new { model = new { Tables = tables.ToArray() } };

        var script = new StringBuilder();
        foreach (var renderer in Templates)
            script.Append(renderer.Render(model));
        return script.ToString();
    }

    protected internal string Render(object model)
    {
        var script = new StringBuilder();
        foreach (var renderer in Templates)
            script.Append(renderer.Render(model));
        return script.ToString();
    }
}
