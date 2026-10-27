using System.Data;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Actual provider construction/configuration plus separately gated Entra database acceptance.</summary>
[NonParallelizable]
public abstract class TokenAuthenticationQaContract : ClientCertificateAuthenticationQaContract
{
    private PasswordQaDriver Driver => new(AuthenticationConnectionUrl, AuthenticationRegistry);
    private static ConnectionUrl Target(PasswordQaDriver driver, TokenQaSource source, TokenQaRegistry registry, bool pooling = false)
        => new ConnectionUrl(driver.WithOptions(pooling), registry).WithAuthentication(SqlServerAuthentication.TokenCallback(source));
    private PasswordQaDriver RequireSqlClient()
    {
        var driver = Driver;
        if (driver.Provider != "Microsoft.Data.SqlClient")
            Assert.Ignore("#1698: " + driver.Provider + " has no native token-callback strategy adapter. Its QA cannot prove SQL Server/Entra authentication.");
        return driver;
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_AvailabilityIsExplicit()
    {
        var driver = Driver;
        using var source = new TokenQaSource();
        var observed = new TokenQaRegistry(driver.Registry);
        if (driver.Provider == "Microsoft.Data.SqlClient")
        {
            await using var connection = await Target(driver, source, observed).ConnectAsync();
            Assert.That(connection, Is.TypeOf<SqlConnection>());
            Assert.That(((SqlConnection)connection).AccessTokenCallback, Is.Not.Null);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
            {
                await using var connection = await Target(driver, source, observed).ConnectAsync();
            });
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
            AssertSafe(error);
            Assert.That(observed.Connections, Is.Empty, "Unsupported providers must reject before URL mapping or native construction.");
            Assert.That(observed.Disposed.Count, Is.EqualTo(observed.Connections.Count));
        }
        Assert.That(source.Calls, Is.Zero);
        Assert.That(source.Disposed, Is.False);
    }

    [TestCase(false), TestCase(true), Category("NativeTokenAuthentication")]
    public async Task TokenQa_ConnectIsLazyAndEnforcesTls(bool asynchronous)
    {
        var driver = RequireSqlClient();
        using var source = new TokenQaSource();
        var observed = new TokenQaRegistry(driver.Registry);
        var target = Target(driver, source, observed);
        _ = target.Parse();
        _ = target.Dialect;
        target.ValidateAuthentication();
        await using (var connection = asynchronous ? await target.ConnectAsync() : (SqlConnection)target.Connect())
        {
            var sql = (SqlConnection)connection;
            var settings = new SqlConnectionStringBuilder(sql.ConnectionString);
            Assert.That(sql.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(sql.AccessTokenCallback, Is.Not.Null);
            Assert.That(sql.AccessToken, Is.Null.Or.Empty);
            Assert.That(settings.Password, Is.Empty);
            Assert.That(settings.UserID, Is.Empty);
            Assert.That(settings.IntegratedSecurity || settings.TrustServerCertificate, Is.False);
            Assert.That(settings.Encrypt, Is.Not.EqualTo(SqlConnectionEncryptOption.Optional));
            Assert.That(source.Calls, Is.Zero);
            Assert.That(target.Parse().Contains(TokenQaSource.Sentinel, StringComparison.Ordinal), Is.False);
        }
        Assert.That(observed.Disposed, Has.Count.EqualTo(1));
        Assert.That(source.Disposed, Is.False);
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_CallbackIdentityIsStableBeforePoolOpening()
    {
        var driver = RequireSqlClient();
        using var source = new TokenQaSource();
        var observed = new TokenQaRegistry(driver.Registry);
        var target = Target(driver, source, observed, pooling: true);
        await using var first = (SqlConnection)await target.ConnectAsync();
        await using var second = (SqlConnection)await target.ConnectAsync();
        await using var separate = (SqlConnection)await Target(driver, source, observed, pooling: true).ConnectAsync();
        Assert.That(first.AccessTokenCallback, Is.SameAs(second.AccessTokenCallback));
        Assert.That(first.AccessTokenCallback, Is.Not.SameAs(separate.AccessTokenCallback));
        Assert.That(new SqlConnectionStringBuilder(first.ConnectionString).Pooling, Is.True);
        Assert.That(source.Calls, Is.Zero, "Closed pooled configuration is not evidence of authenticated pool reuse.");
    }

    [TestCase("userinfo"), TestCase("Password"), TestCase("PWD"), TestCase("AccessToken"), TestCase("Authentication"), TestCase("Integrated Security"), Category("NativeTokenAuthentication")]
    public async Task TokenQa_ConflictsRejectBeforeCreation(string option)
    {
        var driver = Driver;
        using var source = new TokenQaSource();
        var registry = new TokenQaRegistry(driver.Registry);
        var location = driver.Location;
        if (option == "userinfo" && driver.Family is "sqlite" or "duckdb" or "access" or "excel" or "text")
            Assert.Ignore("#1698: file URL grammar does not support userinfo; it rejects that URL before token conflict validation. No credential-conflict pass is substituted.");
        if (option == "userinfo")
        {
            var index = location.IndexOf("://", StringComparison.Ordinal) + 3;
            location = location[..index] + "qa@" + location[index..];
        }
        else location += (location.Contains('?') ? "&" : "?") + Uri.EscapeDataString(option) + "=" + Uri.EscapeDataString(TokenQaSource.Sentinel);
        var target = new ConnectionUrl(location, registry).WithAuthentication(SqlServerAuthentication.TokenCallback(source));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await target.ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        AssertSafe(error);
        Assert.That(source.Calls, Is.Zero);
        Assert.That(registry.Connections, Is.Empty);
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_PreCancelledConnectCreatesNoConnection()
    {
        var driver = Driver;
        using var source = new TokenQaSource();
        var registry = new TokenQaRegistry(driver.Registry);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => { await using var connection = await Target(driver, source, registry).ConnectAsync(cancellation.Token); });
        Assert.That(source.Calls, Is.Zero);
        Assert.That(registry.Connections, Is.Empty);
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_OrdinaryContainerFailureIsSafeAndDisposed()
    {
        var driver = RequireSqlClient();
        if (Environment.GetEnvironmentVariable("DUBURL_ENTRA_QA") == "1")
            Assert.Ignore("#1698: ordinary-container negative QA is separate from the opt-in Entra target.");
        using var source = new TokenQaSource();
        var registry = new TokenQaRegistry(driver.Registry);
        registry.Options["Connect Timeout"] = 5;
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await Target(driver, source, registry).OpenAsync(); });
        AssertSafe(error!);
        Assert.That(registry.Connections, Has.Count.EqualTo(1));
        Assert.That(registry.Disposed, Has.Count.EqualTo(1));
        Assert.That(source.Calls, Is.Zero, "The ordinary SQL fixture must fail before Entra token acquisition. This is not token authentication success.");
        Assert.That(source.Disposed, Is.False);
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_CertificateSourceConfigurationDoesNotAcquireOrDisposeCertificate()
    {
        var driver = RequireSqlClient();
        using var certificate = TokenQaCertificates.Create();
        var source = new EntraCertificateTokenSource("00000000-0000-0000-0000-000000000000",
            new("https://login.microsoftonline.com/qa-tenant"), new("https://database.windows.net/"), certificate);
        using var tracked = new TokenQaSource(source);
        await using (var connection = await Target(driver, tracked, new(driver.Registry)).ConnectAsync())
            Assert.That(((SqlConnection)connection).AccessTokenCallback, Is.Not.Null);
        Assert.That(tracked.Calls, Is.Zero);
        Assert.That(tracked.Disposed, Is.False);
        Assert.That(certificate.HasPrivateKey, Is.True);
    }

    [TestCase("resource"), TestCase("authority"), Category("NativeTokenAuthentication")]
    public void TokenQa_CertificateRequestRejectsUntrustedEndpoint(string mismatch)
    {
        RequireSqlClient();
        using var certificate = TokenQaCertificates.Create();
        var source = new EntraCertificateTokenSource("00000000-0000-0000-0000-000000000000",
            new("https://login.microsoftonline.com/qa-tenant"), new("https://database.windows.net/"), certificate);
        var error = Assert.Throws<ConnectionAuthenticationException>(() => source.ValidateRequest(
            mismatch == "resource" ? "https://untrusted.invalid/" : "https://database.windows.net/",
            mismatch == "authority" ? "https://untrusted.invalid/qa-tenant" : null));
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        AssertSafe(error);
        Assert.That(certificate.HasPrivateKey, Is.True);
    }

    [TestCase(false), TestCase(true), Category("NativeTokenAuthentication")]
    public async Task TokenQa_LiveCertificateAuthenticatesAndQueries(bool asynchronous)
    {
        using var fixture = new TokenQaLiveFixture(RequireSqlClient());
        await using var connection = asynchronous ? await fixture.Target.OpenAsync() : (SqlConnection)fixture.Target.Open();
        await PasswordQaDriver.ProbeAsync(connection, "SELECT 1");
        Assert.That(fixture.Source.Calls, Is.GreaterThan(0));
        Assert.That(fixture.Source.Request, Is.Not.Null);
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_LiveAuthenticatedPoolReusesPhysicalSession()
    {
        using var fixture = new TokenQaLiveFixture(RequireSqlClient(), pooling: true);
        int session;
        await using (var connection = (SqlConnection)await fixture.Target.OpenAsync()) session = connection.ServerProcessId;
        var calls = fixture.Source.Calls;
        Assert.That(calls, Is.GreaterThan(0));
        await using var second = (SqlConnection)await fixture.Target.OpenAsync();
        await PasswordQaDriver.ProbeAsync(second, "SELECT 1");
        Assert.That(second.ServerProcessId, Is.EqualTo(session));
        Assert.That(fixture.Source.Calls, Is.EqualTo(calls));
    }

    [TestCase(false), TestCase(true), Category("NativeTokenAuthentication")]
    public async Task TokenQa_LiveInvalidCertificateFailsWithFreshCache(bool expired)
    {
        using var fixture = new TokenQaLiveFixture(RequireSqlClient());
        using (var positive = await fixture.Target.OpenAsync()) await PasswordQaDriver.ProbeAsync(positive, "SELECT 1");
        using var invalid = TokenQaCertificates.Create(expired);
        using var freshSource = fixture.CreateSource(invalid);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await fixture.TargetFor(freshSource).OpenAsync(); });
        AssertSafe(error!);
        Assert.That(freshSource.Calls, Is.GreaterThan(0));
        Assert.That(fixture.Registry.Disposed.Count, Is.EqualTo(fixture.Registry.Connections.Count));
        Assert.That(invalid.HasPrivateKey, Is.True);
    }

    [TestCase("failure"), TestCase("expired"), TestCase("cancel"), Category("NativeTokenAuthentication")]
    public async Task TokenQa_LiveCallbackFailureDisposesWithoutFallback(string behavior)
    {
        using var fixture = new TokenQaLiveFixture(RequireSqlClient());
        using var source = new TokenQaSource { Behavior = behavior };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var opening = fixture.TargetFor(source).OpenAsync(cancellation.Token).AsTask();
        if (behavior == "cancel")
        {
            await CancelAfterCallbackStartsAsync(source, cancellation, opening);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => { await using var connection = await opening; });
        }
        else AssertSafe((await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await opening; }))!);
        Assert.That(source.Calls, Is.GreaterThan(0));
        Assert.That(source.Request, Is.Not.Null);
        Assert.That(source.Disposed, Is.False);
        Assert.That(fixture.Registry.Disposed.Count, Is.EqualTo(fixture.Registry.Connections.Count));
    }

    [Test, Category("NativeTokenAuthentication")]
    public async Task TokenQa_LiveReplacementCertificateUsesFreshSourceAndPool()
    {
        using var fixture = new TokenQaLiveFixture(RequireSqlClient(), pooling: true);
        using var replacement = TokenQaLiveFixture.LoadCertificate(true);
        Assert.That(replacement.Thumbprint == fixture.Certificate.Thumbprint, Is.False, "Rotation needs two separately registered fixture certificates.");
        Guid previous;
        await using (var connection = (SqlConnection)await fixture.Target.OpenAsync()) previous = connection.ClientConnectionId;
        fixture.Registry.ClearOwnedPools();
        using var source = fixture.CreateSource(replacement);
        var target = fixture.TargetFor(source);
        await using (var connection = (SqlConnection)await target.OpenAsync())
        {
            await PasswordQaDriver.ProbeAsync(connection, "SELECT 1");
            Assert.That(connection.ClientConnectionId, Is.Not.EqualTo(previous));
        }
        var calls = source.Calls;
        Assert.That(calls, Is.GreaterThan(0), "A fresh MSAL client/cache and native callback must use the replacement certificate.");
        await using var reused = await target.OpenAsync();
        await PasswordQaDriver.ProbeAsync(reused, "SELECT 1");
        Assert.That(source.Calls, Is.EqualTo(calls));
    }

    [Test, Category("NativeTokenAuthentication")]
    public void TokenQa_LiveProviderRefreshAfterTokenExpiry()
    {
        RequireSqlClient();
        Assert.Ignore("#1698: no existing QA controls Entra-issued token lifetime/native refresh clock. Real expiry-driven pool renewal is unverified; closed delegate identity or cached-token reuse is not a refresh pass.");
    }

    private static void AssertSafe(ConnectionAuthenticationException error)
        => PasswordQaDriver.AssertSafe(error, TokenQaSource.Sentinel);

    private static async Task CancelAfterCallbackStartsAsync(TokenQaSource source, CancellationTokenSource cancellation, Task<System.Data.Common.DbConnection> opening)
    {
        try { await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch
        {
            cancellation.Cancel();
            try { await using var unexpected = await opening; }
            catch (Exception error) when (error is OperationCanceledException or ConnectionAuthenticationException) { }
            throw;
        }
        cancellation.Cancel();
    }
}
