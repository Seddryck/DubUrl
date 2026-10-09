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
    private IDialect Dialect { get; }
    public bool SupportsComments { get; }

    public SchemaScriptRenderer(IDialect dialect, SchemaCreationOptions options = SchemaCreationOptions.None, NativeTypeRegistry? nativeTypes = null)
    {
        Dialect = dialect;
        SupportsComments = CommentRenderer.Supports(dialect);
        var templates = new List<RendererEngine>();
        if (options == SchemaCreationOptions.DropIfExists)
        {
            templates.Add(new DropTablesIfExistsRenderer(dialect));
            templates.Add(new DropIndexesIfExistsRenderer(dialect));
        }
            
        templates.Add(new CreateSchemaRenderer(dialect, nativeTypes));
        templates.Add(new ForeignKeyRenderer(dialect));
        if (SupportsComments)
            templates.Add(new CommentRenderer(dialect));
        templates.Add(new CreateIndexRenderer(dialect));
        Templates = [.. templates];
    }

    public virtual string Render(Schema schema)
    {
        var tables = new List<TableViewModel>();
        foreach (var table in schema.Tables)
        {
            var tableRenderer = new TableViewModel(table.Value, Dialect is SqliteDialect);
            tables.Add(tableRenderer);
        }
        var indexes = schema.Indexes.Values.Select(index => new IndexViewModel(index)).ToArray();
        var model = new { model = new { Tables = tables.ToArray(), Indexes = indexes } };

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
