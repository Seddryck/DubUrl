using DubUrl.Authentication;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>#1690 CLI consumers, real provider I/O, and separately identified native terminal/store smoke cases.</summary>
[NonParallelizable]
public abstract class CliPasswordAuthenticationQaContract : TokenAuthenticationQaContract
{
    private PasswordQaDriver Driver => new(AuthenticationConnectionUrl, AuthenticationRegistry);

    [TestCase("option"), TestCase("url"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_ControlledPromptAuthenticates(string usernameSource)
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        var location = usernameSource == "url" ? UsernameLocation(driver, driver.Uri.Username) : null;
        await using var harness = new CliPasswordQaHarness(driver, adapters, interactive: true, location: location);
        string[] user = usernameSource == "option" ? ["--username", driver.Uri.Username] : [];
        var result = await harness.CheckAsync(["--password-prompt", .. user, "--engine"]);
        AssertSuccess(result, driver);
        Assert.That(adapters.PromptCalls, Is.EqualTo(1));
        Assert.That(adapters.StoreCalls, Is.Zero);
        AssertOpenedAndDisposed(harness);
    }

    [TestCase("named"), TestCase("environment"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_ControlledStoreAuthenticates(string selector)
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver, adapters);
        var result = await harness.CheckAsync(["--credential", "fixture-entry", "--engine"], selector: selector);
        AssertSuccess(result, driver);
        Assert.That(adapters.StoreCalls, Is.EqualTo(1));
        Assert.That(adapters.PromptCalls, Is.Zero);
        AssertOpenedAndDisposed(harness);
    }

    [TestCase("prompt"), TestCase("store"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_ControlledSourceDeploysSchema(string source)
    {
        var driver = Driver;
        CliPasswordQaSchema.RequireDeployment(driver);
        var schema = new CliPasswordQaSchema();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver, adapters, interactive: true, document: schema.Document);
        try
        {
            var result = await harness.DeployAsync(SourceOptions(driver, source));
            AssertSuccess(result, driver);
            Assert.That(adapters.Calls, Is.EqualTo(1));
            AssertOpenedAndDisposed(harness);
            await schema.VerifyAsync(driver);
        }
        finally { await schema.DropAsync(driver); }
    }

    [TestCase("prompt"), TestCase("store"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_DryRunDoesNotAcquireOrOpen(string source)
    {
        var driver = Driver;
        CliPasswordQaSchema.RequireDryRun(driver);
        var adapters = new CliPasswordQaAdapters(driver.Credentials) { Failure = "dry-run-must-not-read" };
        await using var harness = new CliPasswordQaHarness(driver, adapters, document: new CliPasswordQaSchema().Document);
        var result = await harness.DeployAsync(SourceOptions(driver, source), dryRun: true);
        AssertSuccess(result, driver);
        Assert.That(adapters.Calls, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [TestCase("sources"), TestCase("url-password"), TestCase("credential-option"), TestCase("username-alone"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_ConflictsRejectBeforeAcquisition(string conflict)
    {
        var driver = Driver;
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        var location = driver.WithOptions(false);
        if (conflict == "url-password") location = AddUserInfo(location, "qa:qa-sensitive-conflict-sentinel");
        if (conflict == "credential-option") location += (location.Contains('?') ? "&" : "?") + "Password=qa-sensitive-conflict-sentinel";
        await using var harness = new CliPasswordQaHarness(driver, adapters, interactive: true, location: location);
        string[] options = conflict switch
        {
            "sources" => ["--password-prompt", "--credential", "fixture-entry", "--username", "qa"],
            "username-alone" => ["--username", "qa"],
            _ => ["--password-prompt", "--username", "qa"]
        };
        var result = await harness.CheckAsync(options);
        Assert.That(result.ExitCode, Is.EqualTo(2));
        result.AssertSafe("qa-sensitive-conflict-sentinel");
        Assert.That(adapters.Calls, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [TestCase("option"), TestCase("url"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_UsernameMismatchDoesNotOpen(string identity)
    {
        var driver = Driver;
        driver.RequireStrategy();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        var location = identity == "url" ? UsernameLocation(driver, "qa-mismatched-user") : null;
        await using var harness = new CliPasswordQaHarness(driver, adapters, location: location);
        string[] user = identity == "option" ? ["--username", "qa-mismatched-user"] : [];
        var result = await harness.CheckAsync(["--credential", "fixture-entry", .. user]);
        Assert.That(result.ExitCode, Is.EqualTo(2));
        result.AssertSafe(driver.Uri.Password);
        Assert.That(adapters.StoreCalls, Is.EqualTo(1));
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_PromptWithoutUsernameDoesNotRead()
    {
        var driver = Driver;
        driver.RequireStrategy();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver, adapters, interactive: true);
        var result = await harness.CheckAsync(["--password-prompt"]);
        Assert.That(result.ExitCode, Is.EqualTo(2));
        Assert.That(adapters.Calls, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
        result.AssertSafe(driver.Uri.Password);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_UnattendedPromptDoesNotRead()
    {
        var driver = Driver;
        driver.RequireStrategy();
        var adapters = new CliPasswordQaAdapters(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver, adapters);
        var result = await harness.CheckAsync(SourceOptions(driver, "prompt"));
        Assert.That(result.ExitCode, Is.EqualTo(5));
        Assert.That(adapters.Calls, Is.Zero);
        Assert.That(harness.Observed.Connections, Is.Empty);
        result.AssertSafe(driver.Uri.Password);
    }

    [TestCase("prompt"), TestCase("store"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_AcquisitionCancellationReturns130(string source)
    {
        var driver = Driver;
        driver.RequireStrategy();
        var adapters = new CliPasswordQaAdapters(driver.Credentials) { Block = true };
        await using var harness = new CliPasswordQaHarness(driver, adapters, interactive: true);
        using var cancellation = new CancellationTokenSource();
        var invoking = Task.Run(() => harness.CheckAsync(SourceOptions(driver, source), cancellationToken: cancellation.Token));
        try
        {
            await adapters.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            var result = await invoking.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(result.ExitCode, Is.EqualTo(130));
            Assert.That(adapters.Calls, Is.EqualTo(1));
            Assert.That(harness.Observed.Connections, Is.Empty);
            result.AssertSafe(driver.Uri.Password);
        }
        finally { cancellation.Cancel(); }
    }

    [TestCase("missing"), TestCase("locked"), TestCase("unavailable"), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_ControlledStoreFailuresAreSafe(string failure)
    {
        var driver = Driver;
        driver.RequireStrategy();
        var adapters = new CliPasswordQaAdapters(driver.Credentials) { Failure = failure };
        await using var harness = new CliPasswordQaHarness(driver, adapters);
        var result = await harness.CheckAsync(["--credential", "fixture-entry"]);
        Assert.That(result.ExitCode, Is.EqualTo(5));
        result.AssertJson(false);
        result.AssertSafe("qa-sensitive-store-sentinel");
        result.AssertSafe(driver.Uri.Password);
        Assert.That(adapters.StoreCalls, Is.EqualTo(1));
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [TestCase(false), TestCase(true), Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RejectedStoredCredentialsAreSafeAndDisposed(bool json)
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        var password = Guid.NewGuid().ToString("N") + "!Aa1";
        var adapters = new CliPasswordQaAdapters(new("qa_absent_" + Guid.NewGuid().ToString("N")[..12], password));
        await using var harness = new CliPasswordQaHarness(driver, adapters);
        var result = await harness.CheckAsync(["--credential", "fixture-entry", "--engine"], json: json);
        Assert.That(result.ExitCode, Is.EqualTo(5));
        result.AssertSafe(password);
        if (json) result.AssertJson(false);
        else Assert.That(result.Error.Length, Is.GreaterThan(0));
        AssertOpenedAndDisposed(harness);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealMissingStoreEntryIsSafe()
    {
        var driver = Driver;
        driver.RequireStrategy();
        await using var harness = new CliPasswordQaHarness(driver);
        var result = await harness.CheckAsync(["--credential", "DubUrl-QA-absent-" + Guid.NewGuid().ToString("N"), "--username", driver.Uri.Username]);
        Assert.That(result.ExitCode, Is.EqualTo(5));
        result.AssertJson(false);
        result.AssertSafe(driver.Uri.Password);
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealWindowsCredentialAuthenticates()
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        await using var entry = await CliPasswordQaNativeStore.CreateWindowsAsync(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver);
        var result = await harness.CheckAsync(["--credential", entry.Reference, "--engine"]);
        AssertSuccess(result, driver);
        AssertOpenedAndDisposed(harness);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealWindowsStorePreservesSecret()
    {
        var driver = Driver;
        var secret = "qa_" + Guid.NewGuid().ToString("N") + "=+ ;\r\n\n";
        await using var entry = await CliPasswordQaNativeStore.CreateWindowsAsync(new("qa-user", secret));
        await using var harness = new CliPasswordQaHarness(driver);
        var resolved = await harness.NativeStore.ReadAsync(entry.Reference);
        Assert.That(resolved.Username == "qa-user", Is.True);
        Assert.That(resolved.Password == secret, Is.True, "The real OS entry must preserve all secret characters, including trailing line terminators.");
        Assert.That(harness.Observed.Connections, Is.Empty);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealLinuxSecretServiceAuthenticates()
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        await using var entry = await CliPasswordQaNativeStore.CreateLinuxAsync(driver.Credentials);
        await using var harness = new CliPasswordQaHarness(driver);
        var result = await harness.CheckAsync(["--credential", entry.Reference, "--username", driver.Uri.Username, "--engine"]);
        AssertSuccess(result, driver);
        AssertOpenedAndDisposed(harness);
    }

    [Test, Category("CliPasswordAuthentication")]
    public void CliPasswordQa_RealMacOsKeychainAuthenticates()
        => Assert.Ignore("#1690: no existing provider QA workflow uses macOS and no isolated Keychain fixture is provisioned. Native Keychain retrieval/authentication is unverified; no fake adapter pass is substituted.");

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealHiddenTerminalAuthenticatesWithoutEcho()
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        await CliPasswordQaTerminal.VerifyAsync(driver, GetType().Assembly.Location, escape: false);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealHiddenTerminalEscapeReturns130()
    {
        var driver = Driver;
        driver.RequireStrategy();
        await CliPasswordQaTerminal.VerifyAsync(driver, GetType().Assembly.Location, escape: true);
    }

    [Test, Category("CliPasswordAuthentication")]
    public async Task CliPasswordQa_RealHiddenTerminalDeploysWithoutEcho()
    {
        var driver = Driver;
        CliPasswordQaSchema.RequireDeployment(driver);
        if (!OperatingSystem.IsLinux()) Assert.Ignore("#1690: real hidden-terminal deployment has a Linux PTY harness only; Windows/macOS terminal fixtures are not implemented.");
        var schema = new CliPasswordQaSchema();
        try
        {
            await CliPasswordQaTerminal.VerifyAsync(driver, GetType().Assembly.Location, escape: false, schema);
            await schema.VerifyAsync(driver);
        }
        finally { await schema.DropAsync(driver); }
    }

    private static string[] SourceOptions(PasswordQaDriver driver, string source) => source == "prompt"
        ? ["--password-prompt", "--username", driver.Uri.Username]
        : ["--credential", "fixture-entry"];
    private static string UsernameLocation(PasswordQaDriver driver, string username)
        => AddUserInfo(driver.WithOptions(false), Uri.EscapeDataString(username));
    private static string AddUserInfo(string location, string userInfo)
    {
        var position = location.IndexOf("://", StringComparison.Ordinal) + 3;
        return location[..position] + userInfo + "@" + location[position..];
    }
    private static void AssertSuccess(CliPasswordQaResult result, PasswordQaDriver driver)
    {
        Assert.That(result.ExitCode, Is.Zero, "The real CLI must authenticate/render/deploy successfully; sensitive output is suppressed.");
        result.AssertJson(true);
        result.AssertSafe(driver.Uri.Password);
    }
    private static void AssertOpenedAndDisposed(CliPasswordQaHarness harness)
    {
        Assert.That(harness.Observed.Connections, Has.Count.EqualTo(1));
        Assert.That(harness.Observed.Connections[0].OpenStarted, Is.True);
        Assert.That(harness.Observed.Connections[0].WasDisposed, Is.True);
    }
}
