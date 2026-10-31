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
    private NativeTypeRegistry NativeTypes { get; }
    /// <summary>Whether descriptions can be rendered by this implementation.</summary>
    public bool SupportsComments { get; }
    public bool EngineSupportsComments => (Dialect.Capabilities ?? DialectCapabilities.Default).SupportsComments;

    public SchemaScriptRenderer(IDialect dialect, SchemaCreationOptions options = SchemaCreationOptions.None, NativeTypeRegistry? nativeTypes = null)
    {
        Dialect = dialect;
        NativeTypes = nativeTypes ?? NativeTypeRegistry.Default;
        SupportsComments = CommentRenderer.Supports(dialect);
        var templates = new List<RendererEngine>();
        if (options == SchemaCreationOptions.DropIfExists)
        {
            templates.Add(new DropViewsIfExistsRenderer(dialect));
            templates.Add(new DropIndexesIfExistsRenderer(dialect));
            templates.Add(new DropTablesIfExistsRenderer(dialect));
        }

        templates.Add(new CreateSchemaRenderer(dialect, nativeTypes, options == SchemaCreationOptions.DropIfExists));
        templates.Add(new ForeignKeyRenderer(dialect));
        if (SupportsComments)
            templates.Add(new CommentRenderer(dialect));
        templates.Add(new CreateIndexRenderer(dialect));
        templates.Add(new CreateViewsRenderer(dialect));
        Templates = [.. templates];
    }

    public virtual string Render(Schema schema)
    {
        if (EngineSupportsComments && !SupportsComments && schema.Tables.Values.Any(table =>
            table.Description is not null || table.Columns.Values.Any(column => column.Description is not null)))
            throw new NotSupportedException($"Database comments are supported, but comment rendering is not implemented for '{Dialect.GetType().Name}'. Add a provider ISchemaSqlRenderer comment implementation or omit descriptions.");
        var tables = new List<TableViewModel>();
        var declaredTables = Dialect.SchemaSqlRenderer.RequiresForeignKeyDependencyOrder
            ? TableDependencyPlanner.Order(schema.Tables.Values) : schema.Tables.Values.ToArray();
        foreach (var table in declaredTables)
        {
            var tableRenderer = new TableViewModel(
                table,
                Dialect.SchemaSqlRenderer.RenderForeignKeysInline(Dialect));
            tables.Add(tableRenderer);
        }
        // Validate both endpoints, including metadata for pre-existing reference tables.
        foreach (var table in schema.Tables.Values)
        foreach (var key in table.Constraints.OfType<Constraints.ForeignKeyConstraint>())
        {
            var source = tables.Single(item => item.Identity == table.Identity);
            foreach (var name in key.SourceColumns)
                source.Columns.Single(column => column.Name == name).IsKey = true;
            var target = schema.Tables.TryGetValue(key.TargetTable.Key, out var created)
                ? created : schema.ExternalTables[key.TargetTable.Key];
            foreach (var name in key.TargetColumns)
            {
                var column = new ColumnViewModel(target.Columns[name]);
                var type = CreateSchemaRenderer.RenderColumnType(column, Dialect, NativeTypes);
                type = Dialect.SchemaSqlRenderer.RenderColumnType(Dialect, type, column.DeclaredLength,
                    column.Precision, column.Scale,
                    column.NativeType is not null && NativeTypes.TryResolve(Dialect, column.NativeType, out _));
                Dialect.SchemaSqlRenderer.ValidateKeyColumn(Dialect, type, target.Identity.ToString(), name);
            }
        }
        foreach (var index in schema.Indexes.Values.Where(index => index.IsUnique))
        foreach (var name in index.Columns.Keys)
            tables.Single(table => table.Identity == index.TableIdentity).Columns.Single(column => column.Name == name).IsKey = true;
        var indexes = schema.Indexes.Values.Select(index => new IndexViewModel(index)).ToArray();
        var orderedViews = ViewDependencyPlanner.Order(schema.Views.Values)
            .Select(view => new ViewViewModel(view)).ToArray();
        var model = new
        {
            model = new
            {
                Tables = tables.ToArray(),
                TablesToDrop = tables.AsEnumerable().Reverse().ToArray(),
                Indexes = indexes,
                Views = orderedViews,
                ViewsToDrop = orderedViews.Reverse().ToArray()
            }
        };

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
