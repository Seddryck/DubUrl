using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[NonParallelizable]
public abstract class OperatingSystemIdentityQaContract
{
    protected abstract string AuthenticationConnectionUrl { get; }
    protected abstract ISchemeRegistry AuthenticationRegistry { get; }
    private PasswordQaDriver IdentityDriver => new(AuthenticationConnectionUrl, AuthenticationRegistry);
    private bool SqlIdentity => IdentityDriver.Provider == "Microsoft.Data.SqlClient";
    private bool PeerIdentity => IdentityDriver.Provider == "Npgsql" && IdentityDriver.Family == "postgresql";
    private IdentityQaPeerFixture? peer;
    private IdentityQaLocalDbFixture? localDb;

    private void RequireAdapter()
    {
        if (!SqlIdentity && !PeerIdentity) Assert.Ignore("#1700: no OS-identity adapter for this provider/family/transport. A password login or wire-compatible driver is not OS authentication.");
    }
    private string IdentityLocation => SqlIdentity ? "mssql://localhost/master?Pooling=false"
        : "postgres://alice@localhost:55432/app?Host=%2Ftmp%2Fduburl-qa-peer&Pooling=false";
    private ConnectionUrl IdentityTarget(string url, TokenQaRegistry registry)
        => new ConnectionUrl(url, registry).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());

    [OneTimeTearDown]
    public async Task DisposeIdentityQaResources()
    {
        if (peer is not null) await peer.DisposeAsync();
        if (localDb is not null) await localDb.DisposeAsync();
    }

    [Test, Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_AvailabilityIsExplicit()
    {
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        if (SqlIdentity || (PeerIdentity && !OperatingSystem.IsWindows()))
        {
            await using var connection = await IdentityTarget(IdentityLocation, registry).ConnectAsync();
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            AssertIdentitySettings(connection);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
            { await using var connection = await IdentityTarget(IdentityDriver.Location, registry).ConnectAsync(); });
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
            Assert.That(registry.Connections, Is.Empty);
            AssertSafeIdentity(error);
        }
    }

    [TestCase(false), TestCase(true), Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_MetadataIsLazyAndConnectIsCallerOwned(bool asynchronous)
    {
        RequireAdapter();
        if (PeerIdentity && OperatingSystem.IsWindows()) Assert.Ignore("#1700: Unix socket peer adapter is unsupported on Windows.");
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var original = new ConnectionUrl(IdentityLocation, registry);
        var target = original.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        Assert.That(ReferenceEquals(original, target), Is.False);
        Assert.That(target.Parse(), Is.EqualTo(original.Parse()), "Parse preserves URL-only behavior.");
        _ = target.Dialect;
        Assert.That(registry.Connections, Is.Empty);
        using (var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect())
        {
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            AssertIdentitySettings(connection);
        }
        Assert.That(registry.Connections, Has.Count.EqualTo(1));
    }

    private void AssertIdentitySettings(DbConnection connection)
    {
        var settings = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        Assert.That(settings.ContainsKey("Password"), Is.False);
        if (SqlIdentity)
        {
            var sql = new SqlConnectionStringBuilder(connection.ConnectionString);
            Assert.That(sql.IntegratedSecurity, Is.True);
            Assert.That(sql.UserID, Is.Empty);
            Assert.That(sql.Authentication, Is.EqualTo(SqlAuthenticationMethod.NotSpecified));
        }
        else
        {
            Assert.That(settings["Require Auth"], Is.EqualTo("None"));
            Assert.That(settings["SSL Mode"], Is.EqualTo("Disable"));
            Assert.That(settings["Host"], Is.EqualTo("/tmp/duburl-qa-peer"));
            Assert.That(Convert.ToInt32(settings["Port"]), Is.EqualTo(55432));
            Assert.That(settings["Username"], Is.EqualTo("alice"));
        }
    }

    [TestCase("Password"), TestCase("PWD"), TestCase("Passfile"), TestCase("AccessToken")]
    [TestCase("Authentication"), TestCase("SSL Certificate"), TestCase("SSL Key")]
    [TestCase("Integrated Security"), TestCase("Trusted_Connection")]
    [Category("OperatingSystemIdentityAuthentication")]
    public void OsIdentityQa_CompetingOptionsRejectBeforeCreation(string option)
    {
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var separator = IdentityDriver.Location.Contains('?') ? "&" : "?";
        var value = option is "Integrated Security" or "Trusted_Connection" ? "false" : "";
        var error = Assert.Throws<ConnectionAuthenticationException>(() =>
            IdentityTarget(IdentityDriver.Location + separator + Uri.EscapeDataString(option) + "=" + value, registry).Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafeIdentity(error);
    }

    [TestCase(""), TestCase("qa-sensitive-source-sentinel"), Category("OperatingSystemIdentityAuthentication")]
    public void OsIdentityQa_UrlPasswordRejectsBeforeCreation(string password)
    {
        if (IdentityDriver.Family is "sqlite" or "duckdb" or "access" or "excel" or "text")
            Assert.Ignore("#1700: file URL grammar rejects userinfo before authentication validation; no URL-password strategy case applies.");
        var location = IdentityDriver.Location;
        var at = location.IndexOf("://", StringComparison.Ordinal) + 3;
        location = location[..at] + "alice:" + Uri.EscapeDataString(password) + "@" + location[at..];
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = Assert.Throws<ConnectionAuthenticationException>(() => IdentityTarget(location, registry).Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafeIdentity(error);
    }

    [Test, Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_PreCancelledConnectCreatesNoConnection()
    {
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () =>
        { await using var connection = await IdentityTarget(IdentityDriver.Location, registry).ConnectAsync(cancelled.Token); });
        Assert.That(registry.Connections, Is.Empty);
    }

    [TestCase("true"), TestCase("SSPI"), Category("OperatingSystemIdentityAuthentication")]
    public void OsIdentityQa_SqlCompatibleIntegratedSettingIsRetained(string mode)
    {
        if (!SqlIdentity) Assert.Ignore("#1700: Integrated Security is a SqlClient setting, not a PostgreSQL peer setting.");
        using var connection = IdentityTarget(IdentityLocation + "&Integrated%20Security=" + mode, new(AuthenticationRegistry)).Connect();
        Assert.That(new SqlConnectionStringBuilder(connection.ConnectionString).IntegratedSecurity, Is.True);
    }

    [TestCase(false), TestCase(true), Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_SqlUsernameIsNotImpersonation(bool asynchronous)
    {
        if (!SqlIdentity) Assert.Ignore("#1700: SQL Server rejects a requested username; PostgreSQL permits requested server-mapped roles.");
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var target = IdentityTarget("mssql://alice@localhost/master", registry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        { using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Has.Count.EqualTo(1));
        Assert.That(registry.Disposed, Does.Contain(registry.Connections.Single()));
        AssertSafeIdentity(error);
    }

    [TestCase("localhost"), TestCase("%2Ftmp%2Fa,%2Ftmp%2Fb"), Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_PeerRejectsTcpAndMultiHost(string host)
    {
        if (!PeerIdentity) Assert.Ignore("#1700: local peer endpoint guard applies only to native PostgreSQL.");
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = Assert.Throws<ConnectionAuthenticationException>(() => IdentityTarget("postgres://alice@localhost/app?Host=" + host, registry).Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        AssertSafeIdentity(error);
        if (OperatingSystem.IsWindows()) Assert.That(registry.Connections, Is.Empty);
        else
        {
            Assert.That(registry.Connections, Has.Count.EqualTo(1));
            await CertificateQaDriver.AssertDisposedAsync(registry.Connections.Single());
        }
    }

    [TestCase("sync"), TestCase("async"), TestCase("mapped-role"), TestCase("mismatched-role")]
    [TestCase("omitted-role"), TestCase("pool-reuse"), TestCase("pool-cancellation")]
    [TestCase("reopen"), TestCase("missing-socket"), TestCase("cached-role-isolation")]
    [Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_LivePeer(string scenario)
    {
        if (!PeerIdentity) Assert.Ignore("#1700: PostgreSQL peer QA requires the native PostgreSQL adapter, not a wire-compatible family or transport.");
        if (Environment.GetEnvironmentVariable("DUBURL_PEER_QA_CHILD") != "1")
        {
            peer ??= await IdentityQaPeerFixture.CreateAsync(GetType().Assembly.Location);
            peer.AssertPassed(TestContext.CurrentContext.Test.Name);
            return;
        }
        await ExerciseLiveIdentityAsync("postgres://alice@localhost:55432/app?Host=%2Ftmp%2Fduburl-identity-socket&Timeout=5", scenario, false);
    }

    [TestCase("sync"), TestCase("async"), TestCase("pool-reuse"), TestCase("pool-cancellation")]
    [TestCase("reopen"), TestCase("missing-instance"), Category("OperatingSystemIdentityAuthentication")]
    public async Task OsIdentityQa_LiveLocalDb(string scenario)
    {
        if (!SqlIdentity) Assert.Ignore("#1700: LocalDB QA requires native SqlClient.");
        localDb ??= await IdentityQaLocalDbFixture.CreateAsync();
        await ExerciseLiveIdentityAsync(localDb.Url, scenario, true);
    }

    private async Task ExerciseLiveIdentityAsync(string location, string scenario, bool sql)
    {
        var pooled = scenario is "pool-reuse" or "pool-cancellation" or "cached-role-isolation";
        await using var scope = new IdentityQaConnectionScope(AuthenticationRegistry, pooled);
        if (scenario == "mapped-role") location = location.Replace("alice@", "mapped_alice@", StringComparison.Ordinal);
        if (scenario == "omitted-role") location = location.Replace("alice@", "", StringComparison.Ordinal);
        if (scenario == "mismatched-role") location = location.Replace("alice@", "bob@", StringComparison.Ordinal);
        if (scenario == "missing-socket") location = location.Replace("duburl-identity-socket", "duburl-identity-missing-" + Guid.NewGuid().ToString("N"), StringComparison.Ordinal);
        if (scenario == "missing-instance") location = IdentityQaLocalDbFixture.Location("DubUrlMissing" + Guid.NewGuid().ToString("N"));
        var target = IdentityTarget(location, scope.Registry);
        if (scenario is "mismatched-role" or "missing-socket" or "missing-instance")
        {
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var failed = await target.OpenAsync(); });
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
            AssertSafeIdentity(error);
            Assert.That(scope.Registry.Connections, Has.Count.EqualTo(1));
            await AssertIdentityDisposedAsync(scope.Registry.Connections.Single(), sql);
            return;
        }
        var expected = scenario == "mapped-role" ? "mapped_alice" : "alice";
        int session;
        await using (var connection = scenario == "sync" ? (DbConnection)target.Open() : await target.OpenAsync())
        {
            session = await AssertLiveIdentityAsync(connection, sql, expected);
            if (scenario == "reopen")
            {
                await connection.CloseAsync();
                await connection.OpenAsync();
                await AssertLiveIdentityAsync(connection, sql, expected);
            }
            if (scenario == "pool-cancellation")
            {
                using var budget = new CancellationTokenSource();
                var pending = target.OpenAsync(budget.Token).AsTask();
                await Task.Delay(100);
                Assert.That(pending.IsCompleted, Is.False, "The single-session pool must be occupied before cancellation.");
                budget.Cancel();
                await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () =>
                { await using var failed = await pending.WaitAsync(TimeSpan.FromSeconds(10)); });
                Assert.That(scope.Registry.Connections, Has.Count.EqualTo(2));
                await AssertIdentityDisposedAsync(scope.Registry.Connections[1], sql);
            }
        }
        if (scenario == "pool-reuse")
        {
            await using var next = await target.OpenAsync();
            Assert.That(await AssertLiveIdentityAsync(next, sql, expected), Is.EqualTo(session));
        }
        if (scenario == "cached-role-isolation")
        {
            // A different requested role must never borrow alice's authenticated physical session.
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
            { await using var denied = await IdentityTarget(location.Replace("alice@", "bob@", StringComparison.Ordinal), scope.Registry).OpenAsync(); });
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
            await using var original = await target.OpenAsync();
            Assert.That(await AssertLiveIdentityAsync(original, false, "alice"), Is.EqualTo(session));
        }
    }

    private static async Task<int> AssertLiveIdentityAsync(DbConnection connection, bool sql, string role)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 10;
        command.CommandText = sql
            ? "SELECT CASE WHEN ORIGINAL_LOGIN() = @expected AND ORIGINAL_LOGIN() = SUSER_SNAME() AND ORIGINAL_LOGIN() = SYSTEM_USER AND EXISTS (SELECT 1 FROM sys.dm_exec_connections WHERE session_id=@@SPID AND auth_scheme='NTLM') THEN 1 ELSE 0 END, @@SPID"
            : "SELECT CASE WHEN current_user = '" + role + "' AND session_user = '" + role + "' AND inet_client_addr() IS NULL THEN 1 ELSE 0 END, pg_backend_pid()";
        if (sql && OperatingSystem.IsWindows())
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var parameter = command.CreateParameter();
            parameter.ParameterName = "expected";
            parameter.Value = identity.Name;
            command.Parameters.Add(parameter);
        }
        await using var result = await command.ExecuteReaderAsync();
        Assert.That(await result.ReadAsync(), Is.True);
        Assert.That(result.GetInt32(0), Is.EqualTo(1), "The current security-context identity must match; identity text is deliberately suppressed.");
        return Convert.ToInt32(result.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
    }
    private static async Task AssertIdentityDisposedAsync(DbConnection connection, bool sql)
    {
        if (sql) Assert.That(connection.ConnectionString, Is.Empty, "SqlClient disposal clears the connection string.");
        else await CertificateQaDriver.AssertDisposedAsync(connection);
    }
    private static void AssertSafeIdentity(ConnectionAuthenticationException error)
    {
        Assert.That(error.InnerException, Is.Null);
        Assert.That(error.ToString().Contains("qa-sensitive-source-sentinel", StringComparison.Ordinal), Is.False);
        Assert.That(error.ToString().Contains("alice", StringComparison.Ordinal), Is.False);
    }
}
