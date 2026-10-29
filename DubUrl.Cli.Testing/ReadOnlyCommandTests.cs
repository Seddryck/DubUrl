using System.Data.Common;
using System.Text.Json;
using DubUrl.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Provider.Sqlite.Sql;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Dialects;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

[NonParallelizable]
public class ReadOnlyCommandTests
{
    private string path = null!;
    private string Url => "sqlite:///" + path.Replace('\\', '/');

    [SetUp]
    public void Setup()
    {
        path = Path.Combine(Path.GetTempPath(), "duburl-cli-readonly-" + Guid.NewGuid().ToString("N") + ".db");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE sample(value INTEGER); INSERT INTO sample VALUES (42)";
        command.ExecuteNonQuery();
    }

    [TearDown]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
    }

    private IConfiguration Configuration(string? value = null) => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["ConnectionStrings:source"] = value ?? Url }).Build();

    [TestCase("url")]
    [TestCase("named")]
    [TestCase("environment")]
    public async Task Check_ReadOnly_UsesSharedApiForAllSelectors(string selector)
    {
        var variable = "DUBURL_TEST_READONLY_" + Guid.NewGuid().ToString("N");
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        Environment.SetEnvironmentVariable(variable, Url);
        try
        {
            string[] args = selector switch
            {
                "named" => ["connection", "check", "--connection", "source", "--read-only"],
                "environment" => ["connection", "check", "--connection-env", variable, "--read-only"],
                _ => ["connection", "check", Url + "?Mode=ReadWriteCreate", "--read-only"]
            };
            var code = await CliHost.InvokeAsync(args, services);
            Assert.That(code, Is.EqualTo(CliExitCodes.Success), console.StandardError);
            Assert.That(console.StandardOutput, Does.Contain("native read-only"));
            Assert.That(console.StandardError, Is.Empty);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Check_ReadOnly_NeverCreatesMissingDatabaseEvenWithNativeWriteMode(bool readOnly)
    {
        File.Delete(path);
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var args = new List<string> { "connection", "check", Url + "?Mode=ReadWriteCreate" };
        if (readOnly) args.Add("--read-only");
        var code = await CliHost.InvokeAsync(args.ToArray(), services);
        Assert.That(code, Is.EqualTo(readOnly ? CliExitCodes.ProviderOperationFailed : CliExitCodes.Success));
        Assert.That(File.Exists(path), Is.EqualTo(!readOnly));
    }

    [Test]
    public async Task Inspect_ReadOnly_ExportsSchemaWithoutChangingDatabase()
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var code = await CliHost.InvokeAsync(["schema", "inspect", "--connection", "source", "--basic", "--read-only", "--output-format", "json"], services);
        Assert.That(code, Is.EqualTo(CliExitCodes.Success), console.StandardError);
        using var document = JsonDocument.Parse(console.StandardOutput);
        Assert.That(document.RootElement.GetProperty("tables").GetArrayLength(), Is.EqualTo(1));
    }

    [TestCase("check")]
    [TestCase("inspect")]
    public async Task UnsupportedReadOnly_FailsStrictlyWithoutLeakingCredentials(string command)
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console,
            Configuration("mssql://alice:marker-secret@localhost/app"), providerModules: [new UnsupportedModule()]);
        string[] args = command == "check"
            ? ["connection", "check", "--connection", "source", "--read-only"]
            : ["schema", "inspect", "--connection", "source", "--read-only", "--allow-partial"];
        var code = await CliHost.InvokeAsync(args, services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InvalidInput));
        Assert.That(console.StandardOutput, Is.Empty);
        Assert.That(console.StandardError, Does.Contain("unsupported").And.Not.Contain("marker-secret"));
    }

    [TestCase("connection check")]
    [TestCase("connection check --connection source --connection-env ANY")]
    public async Task InvalidSelectors_AreRejected(string arguments)
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var code = await CliHost.InvokeAsync(arguments.Split(' '), services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InvalidInput));
    }

    [Test]
    public async Task Cancellation_ReturnsCancelledWithoutOpeningDatabase()
    {
        File.Delete(path);
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var code = await CliHost.InvokeAsync(["connection", "check", Url, "--read-only"], services, cancellation.Token);
        Assert.That(code, Is.EqualTo(CliExitCodes.Cancelled));
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public async Task ParserErrors_NeverEchoCredentialBearingUrlTokens()
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var code = await CliHost.InvokeAsync(["connection", "check", Url, "postgres://alice:marker-secret@localhost/app", "--read-only"], services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InvalidInput));
        Assert.That(console.StandardError, Does.Not.Contain("marker-secret"));
        Assert.That(console.StandardOutput, Is.Empty);
    }

    private sealed class TestConsole : ICliConsole
    {
        private readonly StringWriter output = new(), error = new();
        public TextReader In => TextReader.Null;
        public TextWriter Out => output;
        public TextWriter Error => error;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
        public string StandardOutput => output.ToString();
        public string StandardError => error.ToString();
    }

    // Deterministic unsupported route: no SQL Server installation/driver is needed and
    // the resolver cannot fail before the shared capability check under test.
    private sealed class UnsupportedModule : IDubUrlProviderModule
    {
        public void Register(IProviderCatalogBuilder catalog)
        {
            var dialects = new DialectRegistryBuilder();
            dialects.AddDialect(typeof(SqliteDialect), ["mssql"]);
            catalog.AddProvider(new ProviderRegistration(
                "Unsupported read-only route", ["mssql"], "test", () => SqliteFactory.Instance,
                dialects.Build().Get(typeof(SqliteDialect)), () => new NamedParametrizer(),
                context => new SqliteMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, "")));
        }
    }
}
