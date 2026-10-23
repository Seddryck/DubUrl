using System.Text;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Schema.Documents;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Worker.Testing;

public partial class SchemaWorkerMethodsTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Inspect_UsesSharedApplicationSemantics_WithStrictOrPartialResult(bool partial)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "worker-inspect-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (var database = new SqliteConnection("Data Source=" + path))
            {
                await database.OpenAsync();
                await using var command = database.CreateCommand();
                command.CommandText = "CREATE TABLE parent(id INTEGER PRIMARY KEY); CREATE TABLE child(id INTEGER REFERENCES parent(id));";
                await command.ExecuteNonQueryAsync();
            }
            var url = "sqlite:///" + path.Replace('\\', '/');
            await using var services = WorkerComposition.CreateDefault(Configuration(("ConnectionStrings:source", url)));
            using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
                Request(1, "schema.inspect", new { connection = "source", tables = new[] { new { name = "child", schema = "main" } }, basic = true, allowPartial = partial }));
            var result = response.RootElement.GetProperty("result");
            var application = await services.GetRequiredService<ISchemaInspectionService>().InspectAsync(new ConnectionUrl(url),
                new() { Tables = [new("child", "main")], Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys], AllowPartial = partial });
            Assert.Multiple(() =>
            {
                Assert.That(result.GetProperty("success").GetBoolean(), Is.EqualTo(application.IsSuccess));
                Assert.That(result.GetProperty("isComplete").GetBoolean(), Is.False);
                Assert.That(result.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("SDI005"));
                Assert.That(result.GetProperty("document").ValueKind == JsonValueKind.Null, Is.EqualTo(!partial));
            });
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Test]
    public async Task Inspect_ProtocolRequiresConfiguredConnection_AndReportsIndependentCapabilities()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        var registry = services.GetRequiredService<WorkerMethodRegistry>();
        using var invalid = await InvokeAsync(registry, Request(1, "schema.inspect", new { }));
        Assert.That(invalid.RootElement.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32602));
        using var missing = await InvokeAsync(registry, Request(2, "schema.inspect", new { connection = "absent" }));
        Assert.That(Codes(missing), Does.Contain("SDW002"));
        using var caps = await InvokeAsync(registry, """{"jsonrpc":"2.0","id":3,"method":"worker.capabilities"}""");
        Assert.That(caps.RootElement.GetProperty("result").GetProperty("features").GetProperty("schemaDiscovery")
            .GetProperty("strictByDefault").GetBoolean(), Is.True);
    }

    [Test]
    public async Task Inspect_InvalidProvider_IsRedacted()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration(("ConnectionStrings:source", "unknown://user:sensitive@example.invalid/db")));
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(), Request(1, "schema.inspect", new { connection = "source" }));
        Assert.That(Codes(response), Does.Contain("SDI001"));
        Assert.That(response.RootElement.ToString(), Does.Not.Contain("sensitive"));
    }
}
