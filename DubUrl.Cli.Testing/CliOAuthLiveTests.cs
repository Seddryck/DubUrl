using System.Data.Common;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Snowflake.Data.Client;

namespace DubUrl.Cli.Testing;

/// <summary>Real human/tenant/database acceptance. Missing external fixtures are ordinary explicit skips.</summary>
[NonParallelizable, Category("OAuthLive")]
public class CliOAuthLiveTests
{
    [TestCase("entra", "browser"), TestCase("entra", "device-code"), TestCase("snowflake", "browser")]
    public async Task LiveSignInOpensDatabaseAndQueriesThroughCli(string provider, string flow)
    {
        await using var live = new LiveFixture(provider, flow);
        var result = await live.Invoke(["connection", "check", "--connection", "live", "--engine", "--timeout", "180", "--output-format", "json"]);
        Assert.That(result, Is.Zero, "Live OAuth CLI login/query must succeed; private diagnostics are not dumped.");
        using var json = JsonDocument.Parse(live.Console.Output.ToString());
        Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
    }

    [TestCase("entra", "browser"), TestCase("entra", "device-code"), TestCase("snowflake", "browser")]
    public async Task LiveFreshPhysicalConnectionReacquiresAfterCacheClearWhereSupported(string provider, string flow)
    {
        await using var live = new LiveFixture(provider, flow);
        var service = new ReopeningCheck(live.Factory, provider == "entra");
        var result = await live.Invoke(["connection", "check", "--connection", "live", "--engine", "--output-format", "json"], service);
        Assert.That(result, Is.Zero);
        Assert.That(service.Queries, Is.EqualTo(2));
        Assert.That(live.Factory.Created, Has.Count.EqualTo(1), "The same owned session is used on the subsequent native connection.");
    }

    [TestCase("entra", "browser"), TestCase("entra", "device-code"), TestCase("snowflake", "browser")]
    public async Task LiveSchemaDeployAuthenticatesAndItsOwnedTableCanBeQueried(string provider, string flow)
    {
        await using var live = new LiveFixture(provider, flow);
        if (Environment.GetEnvironmentVariable("DUBURL_OAUTH_LIVE_SCHEMA") != "1")
            Assert.Ignore("#1719: live OAuth DDL is not enabled; set DUBURL_OAUTH_LIVE_SCHEMA=1 only for an isolated test database with fixture-owned DDL permissions.");
        var table = "QA_OAUTH_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        live.Console.Document = JsonSerializer.Serialize(new { version = 1, tables = new[] { new { identity = new { name = table }, columns = new[] { new { name = "id", type = "int32" } } } } });
        try
        {
            var deployed = await live.Invoke(["schema", "deploy", "-", "--input-format", "json", "--connection", "live", "--output-format", "json"]);
            Assert.That(deployed, Is.Zero, "Real OAuth schema deployment failed; credential details are suppressed.");
            live.Console.Output.GetStringBuilder().Clear();
            var checkedTable = await live.Invoke(["connection", "check", "--connection", "live", "--table", table, "--timeout", "180", "--output-format", "json"]);
            Assert.That(checkedTable, Is.Zero);
        }
        finally
        {
            // The name is generated here, never supplied by a caller. No existing database/table/user is changed.
            var cleanup = new DropOwnedTable(table, provider == "entra");
            var cleaned = await live.Invoke(["connection", "check", "--connection", "live"], cleanup);
            Assert.That(cleaned, Is.Zero, "The UUID-owned OAuth table must be removed; no shared resources are touched.");
        }
    }

    private sealed class LiveFixture : IAsyncDisposable
    {
        internal LiveConsole Console { get; } = new();
        internal CapturingFactory Factory { get; } = new();
        private readonly ServiceProvider services;
        private readonly string[] options;
        internal LiveFixture(string provider, string flow)
        {
            if (Environment.GetEnvironmentVariable("DUBURL_OAUTH_LIVE") != "1")
                Assert.Ignore("#1719: no live human OAuth/identity/database fixture is enabled. Set DUBURL_OAUTH_LIVE=1 with the documented external settings; ordinary QA databases cannot prove OAuth.");
            var prefix = provider == "entra" ? "DUBURL_OAUTH_ENTRA_" : "DUBURL_OAUTH_SNOWFLAKE_";
            var url = Required(prefix + "URL");
            var parsed = ConnectionUri.Parse(url);
            Assert.That(parsed.HasPassword || parsed.HasUsername, Is.False, "The live URL must contain no credentials.");
            if (!parsed.Options.Any(option => option.Key.Equals("Pooling", StringComparison.OrdinalIgnoreCase) && option.Value.Equals("false", StringComparison.OrdinalIgnoreCase)))
                Assert.Ignore("#1719: live fresh-connection acceptance requires an explicit Pooling=false target; cached sessions do not prove OAuth reacquisition.");
            var configuration = new Dictionary<string, string?> { ["ConnectionStrings:live"] = url };
            if (provider == "entra")
            {
                configuration["Authentication:OAuth:ClientId"] = Required(prefix + "CLIENT_ID");
                configuration["Authentication:OAuth:Authority"] = Required(prefix + "AUTHORITY");
                configuration["Authentication:OAuth:Scopes"] = Environment.GetEnvironmentVariable(prefix + "SCOPES") ?? "https://database.windows.net/.default";
            }
            else
            {
                configuration["Authentication:OAuth:Account"] = Required(prefix + "ACCOUNT");
                configuration["Authentication:OAuth:Scopes"] = "session:role:" + Required(prefix + "ROLE");
            }
            DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
            DbProviderFactories.RegisterFactory("Snowflake.Data", SnowflakeDbFactory.Instance);
            services = CliComposition.CreateDefault(Console, new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(),
                providerModules: [new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(Provider.Snowflake.Sql.SnowflakeDialect).Assembly])], oauthSessionFactory: Factory);
            options = ["--oauth", flow, "--allow-oauth-interaction", "--oauth-timeout", "120"];
        }
        private static string Required(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value)) Assert.Ignore("#1719: missing external live setting " + key + ". Values must remain outside source control and logs.");
            return value!;
        }
        internal Task<int> Invoke(string[] arguments, IConnectionCheckService? check = null)
            => CliHost.InvokeAsync([.. arguments, .. options], check is null ? services : new ReplaceCheck(services, check));
        public ValueTask DisposeAsync() => services.DisposeAsync();
    }
    private sealed class ReplaceCheck(IServiceProvider inner, IConnectionCheckService check) : IServiceProvider
    { public object? GetService(Type type) => type == typeof(IConnectionCheckService) ? check : inner.GetService(type); }
    private sealed class LiveConsole : ICliConsole
    {
        internal StringWriter Output { get; } = new();
        internal string Document { get; set; } = "";
        public TextReader In => new StringReader(Document);
        public TextWriter Out => Output;
        // Only deliberately opted-in human live tests display the short-lived user instructions here.
        public TextWriter Error => System.Console.Error;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
    }
    private sealed class CapturingFactory : ICliOAuthSessionFactory
    {
        internal List<IInteractiveOAuthSession> Created { get; } = [];
        public IInteractiveOAuthSession Create(DbConnection connection, CliOAuthRequest request, ICliConsole console)
        { var session = new NativeCliOAuthSessionFactory().Create(connection, request, console); Created.Add(session); return session; }
    }
    private sealed class ReopeningCheck(CapturingFactory factory, bool clearMsal) : IConnectionCheckService
    {
        internal int Queries { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            await Query(target, cancellationToken);
            if (clearMsal)
            {
                var clear = factory.Created.Single().GetType().GetMethod("ClearCacheAsync")!;
                await (Task)clear.Invoke(factory.Created.Single(), [cancellationToken])!;
            }
            await Query(target, cancellationToken);
            return new(true, "engine", null, 0, []);
        }
        private async Task Query(ConnectionUrl target, CancellationToken token)
        {
            await using var connection = await target.OpenAsync(token);
            await using var command = connection.CreateCommand(); command.CommandText = "SELECT 1";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync(token)), Is.EqualTo(1));
            Queries++;
        }
    }
    private sealed class DropOwnedTable(string table, bool sqlServer) : IConnectionCheckService
    {
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            await using var connection = await target.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE IF EXISTS " + (sqlServer ? "[" + table + "]" : "\"" + table + "\"");
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new(true, "connection", null, 0, []);
        }
    }
}
