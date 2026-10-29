using System.Text.Json;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Actual CLI/provider configuration guards. Ordinary QA passwords are never evidence of OAuth login.</summary>
public abstract class CliOAuthQaContract
{
    protected abstract string AuthenticationConnectionUrl { get; }
    protected abstract ISchemeRegistry AuthenticationRegistry { get; }
    private PasswordQaDriver OAuthDriver => new(AuthenticationConnectionUrl, AuthenticationRegistry);
    private static readonly string[] EntraSettings = ["--oauth-client-id", "11111111-1111-1111-1111-111111111111", "--oauth-tenant", "22222222-2222-2222-2222-222222222222"];

    [TestCase("browser"), TestCase("device-code"), Category("CliOAuthAuthentication")]
    public async Task CliOAuthQa_AvailabilityIsExplicitWithoutSignIn(string flow)
    {
        var driver = OAuthDriver;
        await using var harness = new CliPasswordQaHarness(driver);
        var result = await harness.CheckAsync(["--oauth", flow, .. EntraSettings]);
        var supported = driver.Provider == "Microsoft.Data.SqlClient";
        Assert.That(result.ExitCode, Is.EqualTo(supported ? 5 : 2));
        result.AssertJson(false);
        result.AssertSafe(driver.Uri.Password);
        using var json = JsonDocument.Parse(result.Output);
        Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(),
            Is.EqualTo(supported ? "InteractionRequired" : "UnsupportedAuthentication"));
        Assert.That(harness.Observed.Connections.All(connection => !connection.OpenStarted), Is.True, "No interactive sign-in or database opening is authorized by ordinary QA.");
    }
    [TestCase("--password-prompt"), TestCase("--credential-store", "entry"), Category("CliOAuthAuthentication")]
    public async Task CliOAuthQa_CredentialConflictsDoNotAcquire(params string[] conflict)
    {
        var driver = OAuthDriver;
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver, adapters);
        var result = await harness.CheckAsync(["--oauth", "browser", .. EntraSettings, .. conflict]);
        Assert.That(result.ExitCode, Is.EqualTo(2));
        Assert.That(adapters.Calls, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
        result.AssertJson(false);
        result.AssertSafe(driver.Uri.Password);
    }
    [Test, Category("CliOAuthAuthentication")]
    public async Task CliOAuthQa_DryRunIsUnattendedAndDoesNotOpen()
    {
        var driver = OAuthDriver;
        if (driver.Provider != "Microsoft.Data.SqlClient")
            Assert.Ignore("#1719: this existing QA driver has no supported CLI OAuth schema strategy. Snowflake browser live acceptance is a separate external opt-in suite; ordinary password QA is not OAuth.");
        var schema = new CliPasswordQaSchema();
        await using var harness = new CliPasswordQaHarness(driver, document: schema.Document);
        var result = await harness.DeployAsync(["--oauth", "browser", .. EntraSettings], dryRun: true);
        Assert.That(result.ExitCode, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
        result.AssertJson(true);
        result.AssertSafe(driver.Uri.Password);
    }
    [Test, Category("CliOAuthAuthentication")]
    public async Task CliOAuthQa_PreCancelledCheckDoesNotOpen()
    {
        var driver = OAuthDriver;
        await using var harness = new CliPasswordQaHarness(driver);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var result = await harness.CheckAsync(["--oauth", "device-code", .. EntraSettings], cancellationToken: cancelled.Token);
        Assert.That(result.ExitCode, Is.EqualTo(130));
        Assert.That(harness.Observed.Connections, Is.Empty);
        result.AssertSafe(driver.Uri.Password);
    }
}
