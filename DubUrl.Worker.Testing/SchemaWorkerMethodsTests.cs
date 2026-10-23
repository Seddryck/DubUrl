using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Schema.Documents;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Worker.Testing;

public partial class SchemaWorkerMethodsTests
{
    [Test]
    public async Task Capabilities_AdvertiseSchemaContractAndPolicy()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            """{"jsonrpc":"2.0","id":1,"method":"worker.capabilities"}""");
        var result = response.RootElement.GetProperty("result");
        var schema = result.GetProperty("features").GetProperty("schema");

        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("methods").EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()),
                Is.SupersetOf(new[] { "schema.validate", "schema.render", "schema.deploy" }));
            Assert.That(schema.GetProperty("documentVersions")[0].GetInt32(), Is.EqualTo(1));
            Assert.That(schema.GetProperty("dialects").EnumerateArray()
                .Select(item => item.GetString()), Does.Contain("sqlite"));
            Assert.That(schema.GetProperty("dialects").EnumerateArray()
                .Select(item => item.GetString()), Does.Contain("firebird"));
            Assert.That(schema.GetProperty("deployableSchemes").EnumerateArray()
                .Select(item => item.GetString()), Does.Not.Contain("firebird"));
            Assert.That(schema.GetProperty("providers").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(schema.GetProperty("deploymentEnabled").GetBoolean(), Is.False);
        });
    }

    [Test]
    public async Task Validate_InvalidAndUnsupportedDocuments_ReturnStructuredDiagnostics()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        var registry = services.GetRequiredService<WorkerMethodRegistry>();
        using var invalid = await InvokeAsync(registry,
            Request(1, "schema.validate", new { document = InvalidDocument() }));
        using var unsupported = await InvokeAsync(registry,
            Request(2, "schema.validate", new { document = ValidDocument(2) }));

        Assert.Multiple(() =>
        {
            Assert.That(invalid.RootElement.GetProperty("result").GetProperty("success").GetBoolean(), Is.False);
            Assert.That(Codes(invalid), Does.Contain("SDV002"));
            Assert.That(Codes(unsupported), Does.Contain("SDV001"));
        });
    }

    [Test]
    public async Task Render_ValidDocument_IsDeterministic()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        var registry = services.GetRequiredService<WorkerMethodRegistry>();
        var request = Request(1, "schema.render", new { document = ValidDocument(), dialect = "sqlite" });
        using var first = await InvokeAsync(registry, request);
        using var second = await InvokeAsync(registry, request);

        Assert.Multiple(() =>
        {
            Assert.That(first.RootElement.GetProperty("result").GetProperty("success").GetBoolean(), Is.True);
            Assert.That(first.RootElement.GetProperty("result").GetProperty("sql").GetString(),
                Does.Contain("CREATE TABLE customer"));
            Assert.That(second.RootElement.GetProperty("result").GetProperty("sql").GetString(),
                Is.EqualTo(first.RootElement.GetProperty("result").GetProperty("sql").GetString()));
        });
    }

    [Test]
    public async Task Render_FirebirdDialect_DoesNotRequireBundledProvider()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.render", new { document = ValidDocument(), dialect = "firebird" }));

        Assert.Multiple(() =>
        {
            Assert.That(response.RootElement.GetProperty("result").GetProperty("success").GetBoolean(), Is.True);
            Assert.That(response.RootElement.GetProperty("result").GetProperty("dialect").GetString(),
                Is.EqualTo("firebird"));
        });
    }

    [Test]
    public async Task Deploy_DryRun_DoesNotCreateDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"duburl-worker-{Guid.NewGuid():N}.db");
        var connectionUrl = $"sqlite:///{databasePath.Replace('\\', '/')}";
        await using var services = WorkerComposition.CreateDefault(Configuration(
            ("ConnectionStrings:test", connectionUrl)));

        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.deploy", new { document = ValidDocument(), connection = "test", dryRun = true }));

        Assert.Multiple(() =>
        {
            Assert.That(response.RootElement.GetProperty("result").GetProperty("success").GetBoolean(), Is.True);
            Assert.That(response.RootElement.GetProperty("result").GetProperty("dryRun").GetBoolean(), Is.True);
            Assert.That(File.Exists(databasePath), Is.False);
        });
    }

    [Test]
    public async Task Deploy_LiveDisabled_ReturnsStableDiagnostic()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration(
            ("ConnectionStrings:test", "sqlite:///ignored.db")));
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.deploy", new { document = ValidDocument(), connection = "test" }));

        Assert.That(Codes(response), Does.Contain(SchemaWorkerDiagnosticCodes.DeploymentDisabled));
    }

    [Test]
    public async Task Deploy_DirectConnectionWithoutPolicy_ReturnsStableDiagnosticWithoutEchoingUrl()
    {
        const string connectionUrl = "pgsql://alice:secret@localhost/sample";
        await using var services = WorkerComposition.CreateDefault(Configuration());
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.deploy", new
            {
                document = ValidDocument(),
                connectionUrl,
                dryRun = true
            }));

        Assert.Multiple(() =>
        {
            Assert.That(Codes(response), Does.Contain(SchemaWorkerDiagnosticCodes.DirectConnectionDisabled));
            Assert.That(response.RootElement.ToString(), Does.Not.Contain(connectionUrl));
            Assert.That(response.RootElement.ToString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task Deploy_MissingNamedConnection_ReturnsStableDiagnostic()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.deploy", new
            {
                document = ValidDocument(),
                connection = "missing",
                dryRun = true
            }));

        Assert.That(Codes(response), Does.Contain(SchemaWorkerDiagnosticCodes.ConnectionNotFound));
    }

    [Test]
    public async Task Deploy_DropExistingWithoutPolicy_ReturnsStableDiagnostic()
    {
        await using var services = WorkerComposition.CreateDefault(Configuration());
        using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
            Request(1, "schema.deploy", new
            {
                document = ValidDocument(),
                dialect = "sqlite",
                dryRun = true,
                dropExisting = true
            }));

        Assert.That(Codes(response), Does.Contain(SchemaWorkerDiagnosticCodes.DestructiveDeploymentDisabled));
    }

    [Test]
    public async Task Deploy_EnabledNamedSqliteConnection_CreatesTable()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"duburl-worker-{Guid.NewGuid():N}.db");
        var connectionUrl = $"sqlite:///{databasePath.Replace('\\', '/')}";
        try
        {
            await using var services = WorkerComposition.CreateDefault(Configuration(
                ("Worker:Deployment:Enabled", "true"),
                ("ConnectionStrings:test", connectionUrl)));
            using var response = await InvokeAsync(services.GetRequiredService<WorkerMethodRegistry>(),
                Request(1, "schema.deploy", new { document = ValidDocument(), connection = "test" }));

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='customer'";
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());

            Assert.Multiple(() =>
            {
                Assert.That(response.RootElement.GetProperty("result").GetProperty("success").GetBoolean(), Is.True);
                Assert.That(count, Is.EqualTo(1));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Test]
    public void Redact_RemovesUrlUserInfoAndDecodedCredentials()
    {
        const string connection = "pgsql://alice:p%40ssword@localhost/sample";
        var diagnostics = new[]
        {
            new SchemaOperationDiagnostic("test", SchemaDocumentDiagnosticSeverity.Error,
                $"Failed for {connection}; user alice password p@ssword", "$")
        };

        var redacted = SchemaWorkerMethods.Redact(diagnostics, connection).Single().Message;

        Assert.Multiple(() =>
        {
            Assert.That(redacted, Does.Not.Contain(connection));
            Assert.That(redacted, Does.Not.Contain("alice"));
            Assert.That(redacted, Does.Not.Contain("p@ssword"));
        });
    }

    [Test]
    public async Task Deploy_PropagatesJsonRpcCancellation()
    {
        var deployment = new WaitingDeploymentService();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(Configuration())
            .AddSingleton(new SchemaWorkerPolicy(true, true, false))
            .AddSingleton(new SchemaWorkerCapabilities([1], ["sqlite"], ["sqlite"], [], true, true, false))
            .AddSingleton<ISchemaDeploymentService>(deployment)
            .BuildServiceProvider();
        await using (services)
        {
            var registry = new WorkerMethodRegistry();
            SchemaWorkerMethods.Register(registry, services);
            using var response = await InvokeAsync(registry,
                Request(7, "schema.deploy", new
                {
                    document = ValidDocument(),
                    connectionUrl = "sqlite:///:memory:"
                }),
                """{"jsonrpc":"2.0","method":"$/cancelRequest","params":{"id":7}}""");

            Assert.Multiple(() =>
            {
                Assert.That(response.RootElement.GetProperty("error").GetProperty("code").GetInt32(),
                    Is.EqualTo(-32800));
                Assert.That(deployment.WasCancelled, Is.True);
            });
        }
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(item => item.Key, item => (string?)item.Value)).Build();

    private static object ValidDocument(int version = 1) => new
    {
        version,
        tables = new[]
        {
            new
            {
                identity = new { name = "customer" },
                columns = new[] { new { name = "id", type = "int32", nullable = false } }
            }
        }
    };

    private static object InvalidDocument() => new
    {
        version = 1,
        tables = new[] { new { identity = new { }, columns = Array.Empty<object>() } }
    };

    private static string Request(int id, string method, object parameters)
        => JsonSerializer.Serialize(new { jsonrpc = "2.0", id, method, @params = parameters });

    private static string[] Codes(JsonDocument response)
        => response.RootElement.GetProperty("result").GetProperty("diagnostics")
            .EnumerateArray().Select(item => item.GetProperty("code").GetString()!).ToArray();

    private static async Task<JsonDocument> InvokeAsync(WorkerMethodRegistry registry, params string[] requests)
    {
        var input = new Pipe();
        var output = new Pipe();
        await using var inputStream = input.Reader.AsStream();
        await using var outputStream = output.Writer.AsStream();
        using var outputReader = new StreamReader(output.Reader.AsStream());
        var host = WorkerHost.RunAsync(inputStream, outputStream, registry, "test-version");

        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes(string.Join('\n', requests) + "\n"));
        var response = await outputReader.ReadLineAsync();
        await input.Writer.CompleteAsync();
        await host;
        return JsonDocument.Parse(response!);
    }

    private sealed class WaitingDeploymentService : ISchemaDeploymentService
    {
        public bool WasCancelled { get; private set; }

        public async Task<SchemaDeploymentResult> DeployAsync(
            ISchemaDocument document,
            global::DubUrl.ConnectionUrl connection,
            SchemaDeploymentOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                throw;
            }
        }
    }
}
