using System.Data.Common;
using System.Runtime.Loader;
using System.Text.Json;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

[NonParallelizable]
public class CliOAuthTests
{
    [TestCase("browser"), TestCase("device-code")]
    public async Task ControlledConnectionCheckSelectsFlowExecutesQueryAndDisposes(string flow)
    {
        await using var qa = new OAuthCliHarness();
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--engine", "--output-format", "json", "--oauth", flow, .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.Zero, qa.Console.Errors.ToString());
        Assert.That(qa.Factory.Requests.Single().Flow, Is.EqualTo(flow));
        Assert.That(qa.Factory.Requests.Single().Scopes, Is.EqualTo(new[] { "https://database.windows.net/.default" }));
        Assert.That(qa.Observed.Connections.Single().Statements, Is.EqualTo(new[] { "SELECT 1" }));
        Assert.That(qa.Factory.Created.Single().Disposed, Is.True);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
        using var json = JsonDocument.Parse(qa.Console.Output.ToString());
        Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
    }
    [TestCase("browser", "deploy"), TestCase("device-code", "deploy")]
    [TestCase("browser", "inspect"), TestCase("device-code", "inspect")]
    public async Task ControlledSchemaOperationsUseSelectedSession(string flow, string command)
    {
        await using var qa = new OAuthCliHarness();
        string[] arguments = command == "deploy" ? ["schema", "deploy", "-", "--input-format", "json"] : ["schema", "inspect", "--basic"];
        var result = await qa.Invoke([.. arguments, "--connection", "test", "--output-format", "json", "--oauth", flow, .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.Zero, qa.Console.Errors.ToString());
        Assert.That(qa.Factory.Created.Single().Opens, Is.EqualTo(1));
        Assert.That(qa.Factory.Created.Single().Disposed, Is.True);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
        if (command == "inspect") Assert.That(qa.Observed.Metadata.Configured, Is.True);
        else Assert.That(qa.Observed.Connections.Single().Statements.Single(), Does.Contain("CREATE TABLE"));
        using var json = JsonDocument.Parse(qa.Console.Output.ToString());
        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
    }
    [TestCase("browser"), TestCase("device-code")]
    public async Task DryRunDoesNotCreateSessionOrOpenEvenUnattended(string flow)
    {
        await using var qa = new OAuthCliHarness(interactive: false);
        var result = await qa.Invoke(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run", "--output-format", "json", "--oauth", flow, .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.Zero, qa.Console.Errors.ToString());
        Assert.That(qa.Factory.Created, Is.Empty);
        Assert.That(qa.Observed.Connections, Is.Empty);
    }
    [TestCase("--password-prompt"), TestCase("--credential-store", "entry"), TestCase("--username", "alice")]
    [TestCase("--credential", "entry"), TestCase("--os-identity")]
    public async Task ConflictingCredentialSourcesFailBeforeCreation(params string[] conflict)
    {
        await using var qa = new OAuthCliHarness();
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "browser", .. OAuthCliHarness.EntraOptions, .. conflict]);
        Assert.That(result, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Is.Empty);
        Assert.That(qa.Observed.Connections, Is.Empty);
    }
    [TestCase("mssql://alice:private-access-token@localhost/app")]
    [TestCase("mssql://localhost/app?Password=")]
    [TestCase("mssql://localhost/app?OAuthClientSecret=private-access-token")]
    [TestCase("mssql://localhost/app?Authentication=Active%20Directory%20Integrated")]
    public async Task UrlSecretsAndCompetingModesAreRejectedAndRedacted(string url)
    {
        await using var qa = new OAuthCliHarness(url);
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--output-format", "json", "--oauth", "browser", .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Is.Empty);
        Assert.That(qa.Console.Output.ToString() + qa.Console.Errors, Does.Not.Contain("private-access-token"));
        using var json = JsonDocument.Parse(qa.Console.Output.ToString());
        Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("CredentialConflict"));
    }
    [TestCase("browser"), TestCase("device-code")]
    public async Task UnattendedSignInIsNotLaunchedAndJsonIsSafe(string flow)
    {
        await using var qa = new OAuthCliHarness(interactive: false);
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--output-format", "json", "--oauth", flow, .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.EqualTo(5));
        Assert.That(qa.Factory.Created, Is.Empty);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
        using var json = JsonDocument.Parse(qa.Console.Output.ToString());
        Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("InteractionRequired"));
    }
    [Test]
    public async Task ExplicitDeviceInstructionsUseStderrAndPreserveJsonStdout()
    {
        await using var qa = new OAuthCliHarness(interactive: false);
        qa.Factory.DeviceInstructions = true;
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--engine", "--output-format", "json", "--oauth", "device-code", "--allow-oauth-interaction", .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.Zero);
        Assert.That(qa.Console.Errors.ToString(), Does.Contain("https://microsoft.com/devicelogin").And.Contain("ABCD-1234"));
        Assert.That(qa.Console.Output.ToString(), Does.Not.Contain("ABCD-1234"));
        using var json = JsonDocument.Parse(qa.Console.Output.ToString());
        Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
    }
    [TestCase("configuration", 5), TestCase("denied", 5), TestCase("cancel", 130), TestCase("timeout", 5)]
    public async Task ControlledFailuresAndTimeoutDisposeWithoutUnsafeDetails(string behavior, int expected)
    {
        await using var qa = new OAuthCliHarness();
        qa.Factory.Behavior = behavior;
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--output-format", "json", "--oauth", "device-code", "--oauth-timeout", "1", .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(qa.Factory.Created.Single().Disposed, Is.True);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
        Assert.That(qa.Console.Output.ToString() + qa.Console.Errors, Does.Not.Contain("private-access-token").And.Not.Contain("authorization-code").And.Not.Contain("verifier-sentinel"));
        if (behavior == "timeout") Assert.That(qa.Console.Output.ToString(), Does.Contain("AuthenticationTimeout"));
    }
    [Test]
    public async Task PreCancellationDoesNotCreateSession()
    {
        await using var qa = new OAuthCliHarness();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "device-code", .. OAuthCliHarness.EntraOptions], cancellation.Token);
        Assert.That(result, Is.EqualTo(130));
        Assert.That(qa.Factory.Created, Is.Empty);
    }
    [Test]
    public async Task SnowflakeBrowserUsesPublicClientAndDeviceCodeIsUnsupported()
    {
        await using var qa = new OAuthCliHarness("snowflake://fixture/app");
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "browser"]);
        Assert.That(result, Is.Zero, qa.Console.Errors.ToString());
        var request = qa.Factory.Requests.Single();
        Assert.That(request.ClientId, Is.EqualTo("LOCAL_APPLICATION"));
        Assert.That(request.Authority, Is.EqualTo(new Uri("https://fixture.snowflakecomputing.com")));
        var rejected = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "device-code"]);
        Assert.That(rejected, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Has.Count.EqualTo(1));
    }
    [TestCase("--oauth-timeout", "private-access-token"), TestCase("--oauth", "private-access-token")]
    [TestCase("--oauth-client-id", "private-access-token")]
    public async Task MalformedOptionsAndOrphanIdentitySettingsNeverEchoValues(params string[] invalid)
    {
        await using var qa = new OAuthCliHarness();
        var result = await qa.Invoke(["connection", "check", "--connection", "test", .. invalid]);
        Assert.That(result, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Is.Empty);
        Assert.That(qa.Console.Output.ToString() + qa.Console.Errors, Does.Not.Contain("private-access-token"));
    }
    [TestCase("--oauth-authority", "https://evil.invalid/tenant")]
    [TestCase("--oauth-redirect-uri", "http://0.0.0.0:3000")]
    [TestCase("--oauth-timeout", "0")]
    public async Task UnsafeSettingsRejectWithoutAcquisition(params string[] invalid)
    {
        await using var qa = new OAuthCliHarness();
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "browser", .. OAuthCliHarness.EntraOptions, .. invalid]);
        Assert.That(result, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Is.Empty);
    }
    [Test]
    public async Task ConfigurationSuppliesIdentityAndOptionsOverrideScopes()
    {
        await using var qa = new OAuthCliHarness(configuration: new()
        {
            ["Authentication:OAuth:ClientId"] = "11111111-1111-1111-1111-111111111111",
            ["Authentication:OAuth:Authority"] = "https://login.microsoftonline.com/22222222-2222-2222-2222-222222222222",
            ["Authentication:OAuth:Scopes"] = "https://database.windows.net/.default"
        });
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "device-code", "--oauth-scope", "https://database.usgovcloudapi.net/.default"]);
        Assert.That(result, Is.Zero, qa.Console.Errors.ToString());
        Assert.That(qa.Factory.Requests.Single().Scopes.Single(), Is.EqualTo("https://database.usgovcloudapi.net/.default"));
    }
    [TestCase(false), TestCase(true)]
    public async Task ProductionAdapterConfiguresTheActualNativeLoadContextWithoutSignIn(bool isolated)
    {
        await using var services = CliComposition.CreateDefault(new OAuthCliHarness.ConsoleCapture(false));
        var context = isolated ? new DriverContext() : null;
        try
        {
            var type = context is null ? typeof(SqlConnection) : context.LoadFromAssemblyPath(typeof(SqlConnection).Assembly.Location).GetType(typeof(SqlConnection).FullName!)!;
            using var connection = (DbConnection)Activator.CreateInstance(type)!;
            connection.ConnectionString = "Data Source=localhost;Initial Catalog=app";
            var request = new CliOAuthRequest("Microsoft.Data.SqlClient", "device-code", "11111111-1111-1111-1111-111111111111",
                new("https://login.microsoftonline.com/22222222-2222-2222-2222-222222222222"), new("http://localhost"), ["https://database.windows.net/.default"], null, 120, null);
            var session = services.GetRequiredService<ICliOAuthSessionFactory>().Create(connection, request, new OAuthCliHarness.ConsoleCapture(false));
            using var owner = (IDisposable)session;
            await session.ConfigureAsync(connection, new("Microsoft.Data.SqlClient", "mssql", "localhost", null, "app", null), new()
            { ClientId = request.ClientId, Authority = request.Authority, RedirectUri = request.RedirectUri, Scopes = request.Scopes, Session = session, AllowInteraction = true });
            Assert.That(type.GetProperty("AccessTokenCallback")!.GetValue(connection), Is.Not.Null);
            Assert.That(AssemblyLoadContext.GetLoadContext(session.GetType().Assembly), Is.SameAs(context ?? AssemblyLoadContext.Default));
        }
        finally { context?.Unload(); }
    }
    [Test]
    public async Task OAuthOpeningAllowanceDoesNotConsumeTheQueryBudget()
    {
        await using var qa = new OAuthCliHarness();
        qa.Factory.Behavior = "slow";
        var result = await qa.Invoke(["connection", "check", "--connection", "test", "--engine", "--timeout", "1", "--oauth", "device-code", "--oauth-timeout", "3", .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.Zero);
        Assert.That(qa.Observed.Connections.Single().Statements, Is.EqualTo(new[] { "SELECT 1" }));
    }
    [Test]
    public async Task CallerCancellationDuringSignInDisposesAndReturns130()
    {
        await using var qa = new OAuthCliHarness();
        qa.Factory.Behavior = "timeout";
        using var cancel = new CancellationTokenSource();
        var running = qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "device-code", .. OAuthCliHarness.EntraOptions], cancel.Token);
        await qa.Factory.OpenEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        Assert.That(await running, Is.EqualTo(130));
        Assert.That(qa.Factory.Created.Single().Disposed, Is.True);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
    }
    [TestCase("deploy", "denied", 4), TestCase("inspect", "denied", 5)]
    [TestCase("deploy", "cancel", 130), TestCase("inspect", "cancel", 130)]
    public async Task SchemaFailuresAreSafeAndCancellationStaysDistinct(string command, string behavior, int expected)
    {
        await using var qa = new OAuthCliHarness();
        qa.Factory.Behavior = behavior;
        string[] start = command == "deploy" ? ["schema", "deploy", "-", "--input-format", "json"] : ["schema", "inspect", "--basic"];
        var result = await qa.Invoke([.. start, "--connection", "test", "--output-format", "json", "--oauth", "device-code", .. OAuthCliHarness.EntraOptions]);
        Assert.That(result, Is.EqualTo(expected));
        Assert.That(qa.Factory.Created.Single().Disposed, Is.True);
        Assert.That(qa.Observed.Connections.Single().WasDisposed, Is.True);
        Assert.That(qa.Console.Output.ToString() + qa.Console.Errors, Does.Not.Contain("private-access-token").And.Not.Contain("verifier-sentinel"));
        if (behavior != "cancel") { using var json = JsonDocument.Parse(qa.Console.Output.ToString()); Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.False); }
    }
    [TestCase("https://evil.invalid/devicelogin", "ABCD-1234")]
    [TestCase("https://microsoft.com/devicelogin?device_code=private", "ABCD-1234")]
    [TestCase("https://microsoft.com/devicelogin", "ABCD\nprivate")]
    public async Task UnsafeDeviceInstructionsAreNotPrinted(string url, string code)
    {
        var console = new OAuthCliHarness.ConsoleCapture(false);
        await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await NativeCliOAuthSessionFactory.PresentDeviceCodeAsync(console, url, code, DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None));
        Assert.That(console.Output.ToString() + console.Errors, Is.Empty);
    }
    [Test]
    public async Task RepeatedConflictingFlowSelectionIsRejected()
    {
        await using var qa = new OAuthCliHarness();
        var exit = await qa.Invoke(["connection", "check", "--connection", "test", "--oauth", "browser", "--oauth", "device-code", .. OAuthCliHarness.EntraOptions]);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(qa.Factory.Created, Is.Empty);
    }
    private sealed class DriverContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name)
            => name.Name == typeof(SqlConnection).Assembly.GetName().Name ? LoadFromAssemblyPath(typeof(SqlConnection).Assembly.Location) : null;
    }
}
