using System.Text.Json;
using System.Data.Common;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CliHostTests
{
    private const string JsonSchema = """
        {
          "version": 1,
          "tables": [
            {
              "identity": { "name": "customer" },
              "columns": [ { "name": "id", "type": "int32", "nullable": false } ]
            }
          ]
        }
        """;

    [Test]
    public async Task Validate_FromStandardInput_WritesStableJsonEnvelope()
    {
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "validate", "-", "--input-format", "json", "--output-format", "json"], services);

        using var result = JsonDocument.Parse(console.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(result.RootElement.GetProperty("command").GetString(), Is.EqualTo("schema validate"));
            Assert.That(result.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(result.RootElement.GetProperty("documentVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(console.StandardError, Is.Empty);
        });
    }

    [Test]
    public async Task Render_FromYamlStandardInput_WithOmittedNullableWritesNotNullSql()
    {
        const string yaml = """
            version: 1
            tables:
              - identity:
                  name: customer
                columns:
                  - name: is_active
                    type: boolean
                    default:
                      type: boolean
                      value: "true"
            """;
        var console = new TestConsole(yaml);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "render", "-", "--input-format", "yaml", "--dialect", "sqlite"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StandardOutput, Does.Contain("CREATE TABLE customer"));
            Assert.That(console.StandardOutput, Does.Contain("is_active INTEGER NOT NULL DEFAULT true"));
            Assert.That(console.StandardError, Is.Empty);
        });
    }

    [Test]
    public async Task Validate_InvalidDocument_WritesDiagnosticsToStandardError()
    {
        var console = new TestConsole("{\"version\":1,\"tables\":[{\"identity\":{},\"columns\":[]}]}");
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "validate", "-", "--input-format", "json"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ValidationFailed));
            Assert.That(console.StandardOutput, Is.Empty);
            Assert.That(console.StandardError, Does.Contain("SDV002"));
            Assert.That(console.StandardError, Does.Contain("tables[0].identity.name"));
        });
    }

    [Test]
    public async Task Deploy_DryRun_DoesNotCreateDatabaseFile()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"dry-run-{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:test"] = $"sqlite:///{databasePath.Replace('\\', '/')}"
        }).Build();
        var console = new TestConsole(JsonSchema);
        RegisterSqlite();
        await using var services = CliComposition.CreateDefault(
            console,
            configuration,
            providerModules: [new BuiltInProviderModule()]);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StandardOutput, Does.Contain("CREATE TABLE customer"));
            Assert.That(File.Exists(databasePath), Is.False);
        });
    }

    [Test]
    public async Task Render_UsesDialectAliasesFromCoreWithoutInstalledConnector()
    {
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "render", "-", "--input-format", "json", "--dialect", "postgresql"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StandardOutput, Does.Contain("CREATE TABLE \"customer\""));
            Assert.That(console.StandardError, Is.Empty);
        });
    }

    [Test]
    public async Task Capabilities_FreshCliHasNoInstalledConnectors()
    {
        var console = new TestConsole(string.Empty);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(["capabilities", "--json"], services);

        using var result = JsonDocument.Parse(console.StandardOutput);
        var providers = result.RootElement.GetProperty("providers")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var dialects = result.RootElement.GetProperty("dialects")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var deployableSchemes = result.RootElement.GetProperty("deployableSchemes")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(providers, Is.Empty);
            Assert.That(dialects, Does.Contain("firebird"));
            Assert.That(deployableSchemes, Is.Empty);
            Assert.That(deployableSchemes, Does.Not.Contain("firebird"));
        });
    }

    [Test]
    public async Task Render_FirebirdAlias_UsesCoreDialectRegistry()
    {
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "render", "-", "--input-format", "json", "--dialect", "firebird"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(console.StandardOutput, Does.Contain("CREATE TABLE \"customer\""));
            Assert.That(console.StandardError, Is.Empty);
        });
    }

    [Test]
    public async Task Render_OutputFile_WritesSqlWithoutMixingStandardOutput()
    {
        var output = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"schema-{Guid.NewGuid():N}.sql");
        try
        {
            var console = new TestConsole(JsonSchema);
            await using var services = CliComposition.CreateDefault(console);

            var exitCode = await CliHost.InvokeAsync(
                ["schema", "render", "-", "--input-format", "json", "--dialect", "sqlite", "--output", output], services);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(console.StandardOutput, Is.Empty);
                Assert.That(File.ReadAllText(output), Does.Contain("CREATE TABLE customer"));
            });
        }
        finally
        {
            if (File.Exists(output)) File.Delete(output);
        }
    }

    [Test]
    public async Task Render_InvalidOutputFormat_DoesNotWriteOutputFile()
    {
        var output = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"invalid-output-{Guid.NewGuid():N}.sql");
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "render", "-", "--input-format", "json", "--dialect", "sqlite", "--output", output,
                "--output-format", "xml"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidInput));
            Assert.That(File.Exists(output), Is.False);
            Assert.That(console.StandardError, Does.Contain("--output-format"));
        });
    }

    [Test]
    public async Task Deploy_ToSqlite_CreatesTable()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"deploy-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = CreateConnectionConfiguration(databasePath);
            var console = new TestConsole(JsonSchema);
            RegisterSqlite();
            await using var services = CliComposition.CreateDefault(
                console,
                configuration,
                providerModules: [new BuiltInProviderModule()]);

            var exitCode = await CliHost.InvokeAsync(
                ["schema", "deploy", "-", "--input-format", "json", "--connection", "test"], services);

            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'customer'";
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(Convert.ToInt64(command.ExecuteScalar()), Is.EqualTo(1));
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Test]
    public async Task Deploy_DestructiveNonInteractive_RequiresExplicitConfirmation()
    {
        var databasePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"confirm-{Guid.NewGuid():N}.db");
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console, CreateConnectionConfiguration(databasePath));

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--drop-existing"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidInput));
            Assert.That(console.StandardError, Does.Contain("requires --yes"));
            Assert.That(File.Exists(databasePath), Is.False);
        });
    }

    [Test]
    public async Task CancelledInvocation_ReturnsDocumentedExitCode()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "validate", "-", "--input-format", "json"], services, cancellation.Token);

        Assert.That(exitCode, Is.EqualTo(CliExitCodes.Cancelled));
    }

    [Test]
    public async Task Deploy_InvalidCredentialBearingUrl_IsRedacted()
    {
        const string secretUrl = "not-a-provider://user:super-secret@example.invalid/database";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:test"] = secretUrl
        }).Build();
        var console = new TestConsole(JsonSchema);
        await using var services = CliComposition.CreateDefault(console, configuration);

        var exitCode = await CliHost.InvokeAsync(
            ["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.InvalidInput));
            Assert.That(console.StandardError, Does.Not.Contain(secretUrl));
            Assert.That(console.StandardError, Does.Not.Contain("super-secret"));
        });
    }

    private static IConfiguration CreateConnectionConfiguration(string databasePath)
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:test"] = $"sqlite:///{databasePath.Replace('\\', '/')}"
        }).Build();

    private static void RegisterSqlite()
        => DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);

    private sealed class TestConsole(string input) : global::DubUrl.Cli.ICliConsole
    {
        private readonly StringWriter output = new();
        private readonly StringWriter error = new();

        public TextReader In { get; } = new StringReader(input);
        public TextWriter Out => output;
        public TextWriter Error => error;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
        public string StandardOutput => output.ToString();
        public string StandardError => error.ToString();
    }
}
