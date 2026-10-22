using System.Data.Common;
using System.Text;
using DubUrl.Schema.Documents;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class SchemaInspectCommandTests
{
    private string path = null!;
    [SetUp]
    public void Setup()
    {
        path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "inspection-" + Guid.NewGuid().ToString("N") + ".db");
        using var connection = new SqliteConnection("Data Source=" + path);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE "parent.table"(b INTEGER NOT NULL,a INTEGER NOT NULL,title TEXT,PRIMARY KEY(b,a));
            CREATE TABLE child(x INTEGER,y INTEGER,FOREIGN KEY(x,y) REFERENCES "parent.table"(b,a));
            """;
        command.ExecuteNonQuery();
    }
    [TearDown]
    public void Cleanup() { SqliteConnection.ClearAllPools(); File.Delete(path); }

    [TestCase("json", SchemaDocumentFormat.Json)]
    [TestCase("yaml", SchemaDocumentFormat.Yaml)]
    [TestCase(null, SchemaDocumentFormat.Yaml)]
    public async Task Inspect_Stdout_IsOnlyReadableDocument(string? format, SchemaDocumentFormat expected)
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var arguments = new List<string> { "schema", "inspect", "--connection", "source", "--basic" };
        if (format is not null) arguments.AddRange(["--output-format", format]);
        var code = await CliHost.InvokeAsync(arguments.ToArray(), services);
        Assert.That(code, Is.EqualTo(CliExitCodes.Success), console.StandardError);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(console.StandardOutput));
        var document = new SchemaDocumentReader().Read(stream, expected);
        Assert.That(new SchemaDocumentValidator().Validate(document), Is.Empty);
        Assert.That(console.StandardError, Is.Empty);
    }

    [TestCase(".json", null, SchemaDocumentFormat.Json)]
    [TestCase(".yml", null, SchemaDocumentFormat.Yaml)]
    [TestCase(".yaml", "json", SchemaDocumentFormat.Json)]
    [TestCase(".txt", "yaml", SchemaDocumentFormat.Yaml)]
    public async Task Inspect_File_InfersFormatAndExplicitFormatWins(string extension, string? format, SchemaDocumentFormat expected)
    {
        var output = path + extension;
        try
        {
            var console = new TestConsole();
            await using var services = CliComposition.CreateDefault(console, Configuration());
            var arguments = new List<string> { "schema", "inspect", "--connection", "source", "--basic", "--output", output };
            if (format is not null) arguments.AddRange(["--output-format", format]);
            var code = await CliHost.InvokeAsync(arguments.ToArray(), services);
            Assert.That(code, Is.EqualTo(0), console.StandardError);
            Assert.That(console.StandardOutput, Is.Empty);
            using var stream = File.OpenRead(output);
            Assert.That(new SchemaDocumentValidator().Validate(new SchemaDocumentReader().Read(stream, expected)), Is.Empty);
        }
        finally { File.Delete(output); }
    }

    [Test]
    public async Task Inspect_RepeatedQuotedFilters_AndEnvironmentConnection()
    {
        var variable = "DUBURL_INSPECTION_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, Url());
        try
        {
            var console = new TestConsole();
            await using var services = CliComposition.CreateDefault(console, Configuration());
            var code = await CliHost.InvokeAsync(["schema","inspect","--connection-env",variable,"--basic",
                "--table","main.\"parent.table\"","--table","main.child","--output-format","json"], services);
            Assert.That(code, Is.EqualTo(0), console.StandardError);
            using var json = System.Text.Json.JsonDocument.Parse(console.StandardOutput);
            Assert.That(json.RootElement.GetProperty("tables").GetArrayLength(), Is.EqualTo(2));
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Inspect_FilteredExternalReference_StrictAndPartialHaveSeparateOutput(bool partial)
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var arguments = new List<string> { "schema", "inspect", "--connection", "source", "--basic", "--table", "main.child", "--output-format", "json" };
        if (partial) arguments.Add("--allow-partial");
        var code = await CliHost.InvokeAsync(arguments.ToArray(), services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InspectionIncomplete));
        Assert.That(console.StandardError, Does.Contain("SDI005"));
        Assert.That(string.IsNullOrEmpty(console.StandardOutput), Is.EqualTo(!partial));
    }

    [TestCase("schema inspect")]
    [TestCase("schema inspect --connection source --connection-env ANY")]
    [TestCase("schema inspect --connection source --output unknown.txt")]
    [TestCase("schema inspect --connection source --output-format xml")]
    public async Task Inspect_InvalidArguments_AreRejected(string arguments)
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        var code = await CliHost.InvokeAsync(arguments.Split(' '), services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InvalidInput));
        Assert.That(console.StandardOutput, Is.Empty);
    }

    [Test]
    public async Task Inspect_UnsupportedAndInvalidConnections_DoNotEchoCredentials()
    {
        var console = new TestConsole();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:source"] = "unknown://user:sensitive@example.invalid/db"
        }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration);
        var code = await CliHost.InvokeAsync(["schema", "inspect", "--connection", "source"], services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InspectionUnsupported));
        Assert.That(console.StandardOutput, Is.Empty);
        Assert.That(console.StandardError, Does.Not.Contain("sensitive"));
    }

    [Test]
    public async Task Inspect_MissingDatabase_IsNotCreated()
    {
        var missing = path + ".missing";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:source"] = "sqlite:///" + missing.Replace('\\', '/') }).Build();
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, configuration);
        var code = await CliHost.InvokeAsync(["schema", "inspect", "--connection", "source", "--basic"], services);
        Assert.That(code, Is.EqualTo(CliExitCodes.InspectionFailed));
        Assert.That(File.Exists(missing), Is.False);
        Assert.That(console.StandardOutput, Is.Empty);
    }

    [Test]
    public async Task Inspect_PreCancelled_ReturnsCancellationCodeWithoutOutput()
    {
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, Configuration());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var code = await CliHost.InvokeAsync(["schema", "inspect", "--connection", "source"], services, cancellation.Token);
        Assert.That(code, Is.EqualTo(CliExitCodes.Cancelled));
        Assert.That(console.StandardOutput, Is.Empty);
    }
    private string Url() => "sqlite:///" + path.Replace('\\', '/');
    private IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["ConnectionStrings:source"] = Url() }).Build();
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
}
