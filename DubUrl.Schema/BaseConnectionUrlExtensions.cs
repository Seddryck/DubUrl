using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Renderers;

namespace DubUrl.Schema;
public static class ConnectionUrlExtensions
{
    public static void DeploySchema(this ConnectionUrl connectionUrl, Schema schema, SchemaCreationOptions options = SchemaCreationOptions.None)
    {
        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = ScriptSchema(connectionUrl, schema, options);
        cmd.ExecuteNonQuery();
    }

    public static void DeploySchema(this ConnectionUrl connectionUrl, Func<ITableCollectionBuilder, ISchemaBuilder> builder, SchemaCreationOptions options = SchemaCreationOptions.None)
        => DeploySchema(connectionUrl, builder(new SchemaBuilder()).Build(), options);

    public static string ScriptSchema(this ConnectionUrl connectionUrl, Schema schema, SchemaCreationOptions options = SchemaCreationOptions.None)
    {
        var tables = new List<TableRender>();
        foreach (var table in schema.Tables)
        {
            var tableRenderer = new TableRender(table.Value);
            tables.Add(tableRenderer);
        }
        var model = new { model = new { Tables = tables.ToArray() } };

        var renderer = new SchemaRenderer(connectionUrl.Dialect, options);
        var sql = renderer.Render(model);

        return sql;
    }

    public static string ScriptSchema(this ConnectionUrl connectionUrl, Func<ITableCollectionBuilder, ISchemaBuilder> builder, SchemaCreationOptions options = SchemaCreationOptions.None)
        => ScriptSchema(connectionUrl, builder(new SchemaBuilder()).Build(), options);
}
