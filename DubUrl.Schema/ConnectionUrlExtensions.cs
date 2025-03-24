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
        var script = ScriptSchema(connectionUrl, schema, options);
        var deployer = new SchemaDeployer();
        deployer.DeploySchema(connectionUrl, script);
    }

    public static void DeploySchema(this ConnectionUrl connectionUrl, Func<ITableCollectionBuilder, ISchemaBuilder> builder, SchemaCreationOptions options = SchemaCreationOptions.None)
        => DeploySchema(connectionUrl, builder(new SchemaBuilder()).Build(), options);

    public static string ScriptSchema(this ConnectionUrl connectionUrl, Schema schema, SchemaCreationOptions options = SchemaCreationOptions.None)
    {
        var renderer = new SchemaRenderer(connectionUrl.Dialect, options);
        var sql = renderer.Render(schema);
        return sql;
    }

    public static string ScriptSchema(this ConnectionUrl connectionUrl, Func<ITableCollectionBuilder, ISchemaBuilder> builder, SchemaCreationOptions options = SchemaCreationOptions.None)
        => ScriptSchema(connectionUrl, builder(new SchemaBuilder()).Build(), options);
}
