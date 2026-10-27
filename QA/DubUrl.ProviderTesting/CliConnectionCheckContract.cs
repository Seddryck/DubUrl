using System.Data;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Cli;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>The same command/parser/output contract, executed against each QA fixture's real provider.</summary>
[NonParallelizable]
public abstract class CliConnectionCheckContract
{
    protected abstract string CliConnectionUrl { get; }
    protected virtual ISchemeRegistry CliRegistry => DefaultRegistry.Get();
    protected virtual string CliTableName => "Customer";
    protected virtual bool CliCanCreateTable => true;

    private string Family => ConnectionCheckSql.GetFamily(ConnectionUri.Parse(CliConnectionUrl).Scheme.Canonical);
    private bool UnsupportedFileDriver => Family is "access" or "excel" or "text"
        || (Family is "sqlite" or "duckdb" && ConnectionUri.Parse(CliConnectionUrl).Scheme.Parts.Count > 1);
    private bool SqlSupported => Family != "unknown" && !UnsupportedFileDriver;

    [TestCase("url"), TestCase("named"), TestCase("environment"), Category("ConnectionCheck")]
    public async Task CliCheckConnection_Json_ResolvesEachTargetSelector(string selector)
    {
        var result = await InvokeAsync([], selector);
        AssertEnvelope(result, "connection", !UnsupportedFileDriver);
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckConnection_Human_UsesCorrectOutputStream()
    {
        var result = await InvokeAsync([], json: false);
        Assert.That(result.ExitCode, Is.EqualTo(UnsupportedFileDriver ? 2 : 0));
        Assert.That(UnsupportedFileDriver ? result.Output.Length == 0 : result.Error.Length == 0, Is.True);
        Assert.That(UnsupportedFileDriver ? result.Error.Length > 0 : result.Output.Length > 0, Is.True);
        AssertRedacted(result);
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckEngine_Json_ExecutesOrRejectsUnsupportedSql()
        => AssertEnvelope(await InvokeAsync(["--engine"]), "engine", SqlSupported);

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckNonemptyTable_Json_AcceptsZeroRowStatementWithoutChangingData()
    {
        var before = SqlSupported ? CountRows(CliTableName) : 0;
        if (SqlSupported) Assert.That(before, Is.GreaterThan(0), "The existing Customer fixture must contain rows.");
        AssertEnvelope(await InvokeAsync(["--table", CliTableName]), "table", SqlSupported);
        if (SqlSupported) Assert.That(CountRows(CliTableName), Is.EqualTo(before));
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckEmptyTable_Json_AcceptsZeroRows()
    {
        if (!SqlSupported)
        {
            AssertEnvelope(await InvokeAsync(["--table", CliTableName]), "table", false);
            return;
        }
        if (!CliCanCreateTable) Assert.Ignore("This fixture exposes read-only external tables and cannot provision an empty table.");
        var table = "duburl_check_" + Guid.NewGuid().ToString("N");
        ExecuteFixtureSql($"CREATE TABLE {QuoteTable(table)} (id INTEGER)");
        try
        {
            Assert.That(CountRows(table), Is.Zero);
            AssertEnvelope(await InvokeAsync(["--table", table]), "table", true);
            Assert.That(CountRows(table), Is.Zero);
        }
        finally { ExecuteFixtureSql($"DROP TABLE {QuoteTable(table)}"); }
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckMissingTable_Json_ReturnsSafeFailure()
    {
        var result = await InvokeAsync(["--table", "duburl_absent_" + Guid.NewGuid().ToString("N")]);
        if (!SqlSupported) { AssertEnvelope(result, "table", false); return; }
        Assert.That(result.ExitCode, Is.EqualTo(5));
        using var json = ReadEnvelope(result, "table", false);
        var code = json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString();
        Assert.That(ExpectedMissingCodes().Contains(code, StringComparer.Ordinal), Is.True,
            "Missing-table diagnostics must match the provider/transport's conservative mapping.");
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckMissingTable_Human_DoesNotLeakConnection()
    {
        var result = await InvokeAsync(["--table", "duburl_absent_" + Guid.NewGuid().ToString("N")], json: false);
        Assert.That(result.ExitCode, Is.EqualTo(SqlSupported ? 5 : 2));
        Assert.That(result.Output.Length, Is.Zero);
        Assert.That(result.Error.Length, Is.GreaterThan(0));
        AssertRedacted(result);
    }

    [Category("ConnectionCheck")]
    [TestCase("--engine", "--table", "Customer")]
    [TestCase("--table", "Customer; DROP TABLE Customer")]
    [TestCase("--table", "Customer WHERE 1=1")]
    [TestCase("--timeout", "0")]
    [TestCase("--timeout", "-1")]
    [TestCase("--connection", "another-target")]
    [TestCase("--output-format", "unsupported")]
    public async Task CliCheckInvalidArguments_ReturnInvalidInput(params string[] arguments)
    {
        var result = await InvokeAsync(arguments);
        Assert.That(result.ExitCode, Is.EqualTo(2));
        AssertRedacted(result);
    }

    [Test, Category("ConnectionCheck")]
    public async Task CliCheckCancellation_Returns130()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await InvokeAsync([], cancellationToken: cancellation.Token);
        Assert.That(result.ExitCode, Is.EqualTo(130));
        AssertRedacted(result);
    }

    private string[] ExpectedMissingCodes()
    {
        if (Family == "sqlite") return ["tableNotFound"];
        if (ConnectionUri.Parse(CliConnectionUrl).Scheme.Parts.Count > 1) return ["executionFailed", "tableNotFoundOrNotAccessible"];
        if (Family is "duckdb" or "trino") return ["executionFailed"];
        return ["tableNotFoundOrNotAccessible"];
    }
    private void AssertEnvelope(CommandResult result, string mode, bool success)
    {
        Assert.That(result.ExitCode, Is.EqualTo(success ? 0 : 2));
        using var json = ReadEnvelope(result, mode, success);
        Assert.That(result.Error.Length, Is.Zero);
        if (!success)
            Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("invalidInput"));
    }
    private JsonDocument ReadEnvelope(CommandResult result, string mode, bool success)
    {
        AssertRedacted(result);
        JsonDocument json;
        try { json = JsonDocument.Parse(result.Output); }
        catch (JsonException) { throw new AssertionException("CLI output must be one valid JSON envelope."); }
        Assert.That(json.RootElement.GetProperty("command").GetString(), Is.EqualTo("connection check"));
        Assert.That(json.RootElement.GetProperty("mode").GetString(), Is.EqualTo(mode));
        Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.EqualTo(success));
        Assert.That(json.RootElement.GetProperty("elapsedMilliseconds").GetInt64(), Is.GreaterThanOrEqualTo(0));
        return json;
    }
    private void AssertRedacted(CommandResult result)
    {
        var text = result.Output + result.Error;
        var uri = ConnectionUri.Parse(CliConnectionUrl);
        Assert.That(text.Contains(CliConnectionUrl, StringComparison.Ordinal), Is.False, "CLI output exposed its connection URL.");
        if (uri.Password.Length == 0) return;
        Assert.That(text.Contains(uri.Password, StringComparison.Ordinal), Is.False, "CLI output exposed a password.");
        Assert.That(text.Contains(Uri.EscapeDataString(uri.Password), StringComparison.Ordinal), Is.False, "CLI output exposed an encoded password.");
    }

    private async Task<CommandResult> InvokeAsync(string[] options, string selector = "url", bool json = true, CancellationToken cancellationToken = default)
    {
        var variable = "DUBURL_CONNECTION_CHECK_QA_" + Guid.NewGuid().ToString("N");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:qa"] = CliConnectionUrl }).Build();
        var console = new RecordingConsole();
        using var services = new ServiceCollection().AddSingleton<ICliConsole>(console).AddSingleton<IConfiguration>(configuration)
            .AddSingleton(CliRegistry).AddSingleton<IConnectionCheckService, ConnectionCheckService>().BuildServiceProvider();
        if (selector == "environment") Environment.SetEnvironmentVariable(variable, CliConnectionUrl);
        try
        {
            var target = selector switch
            {
                "named" => new[] { "--connection", "qa" },
                "environment" => ["--connection-env", variable],
                _ => [CliConnectionUrl]
            };
            string[] format = json ? ["--output-format", "json"] : [];
            var exit = await CliHost.InvokeAsync(["connection", "check", .. target, .. format, .. options], services, cancellationToken);
            return new(exit, console.Output.ToString(), console.Errors.ToString());
        }
        finally { if (selector == "environment") Environment.SetEnvironmentVariable(variable, null); }
    }
    private string QuoteTable(string table)
    {
        var statement = ConnectionCheckSql.Table(Family, table);
        return statement["SELECT 1 FROM ".Length..^" WHERE 1=0".Length];
    }
    private long CountRows(string table)
    {
        using var connection = new ConnectionUrl(CliConnectionUrl, CliRegistry).Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteTable(table)}";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
    private void ExecuteFixtureSql(string sql)
    {
        using var connection = new ConnectionUrl(CliConnectionUrl, CliRegistry).Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    private sealed record CommandResult(int ExitCode, string Output, string Error);
    private sealed class RecordingConsole : ICliConsole
    {
        internal StringWriter Output { get; } = new();
        internal StringWriter Errors { get; } = new();
        public TextReader In => TextReader.Null;
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
    }
}
