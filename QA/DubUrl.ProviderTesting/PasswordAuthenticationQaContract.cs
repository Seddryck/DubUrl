using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using DubUrl.Authentication;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>The #1689 strategy against actual provider connections, with explicit capability/prerequisite exclusions.</summary>
[NonParallelizable]
public abstract class PasswordAuthenticationQaContract : CliPasswordAuthenticationQaContract
{
    private PasswordQaDriver Driver => new(AuthenticationConnectionUrl, AuthenticationRegistry);

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_AvailabilityIsExplicit()
    {
        var driver = Driver;
        using var source = new PasswordQaSource(driver.Credentials);
        var observed = new PasswordQaRegistry(driver.Registry);
        if (driver.SupportsStrategy)
        {
            await using var connection = await driver.Target(source, observed).ConnectAsync();
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(source.Calls, Is.EqualTo(1));
        }
        else
        {
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await driver.Target(source, observed).ConnectAsync());
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
            Assert.That(source.Calls, Is.Zero);
            Assert.That(observed.Connections, Is.Empty);
        }
    }

    [TestCase(false), TestCase(true), Category("PasswordAuthentication")]
    public async Task PasswordStrategy_AuthenticatesAndQueriesDatabase(bool asynchronous)
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        using var source = new PasswordQaSource(driver.Credentials);
        var target = driver.Target(source);
        await using var connection = asynchronous ? await target.OpenAsync() : (DbConnection)target.Open();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        await PasswordQaDriver.ProbeAsync(connection, driver.ProbeSql);
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(source.Disposed, Is.False, "The application owns its credential source.");
        Assert.That(target.Url, Is.EqualTo(driver.WithOptions(false)));
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_ReopeningReturnedConnectionDoesNotResolveAgain()
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        using var source = new PasswordQaSource(driver.Credentials);
        await using var connection = await driver.Target(source).ConnectAsync();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        await connection.OpenAsync();
        await PasswordQaDriver.ProbeAsync(connection, driver.ProbeSql);
        await connection.CloseAsync();
        await connection.OpenAsync();
        await PasswordQaDriver.ProbeAsync(connection, driver.ProbeSql);
        Assert.That(source.Calls, Is.EqualTo(1));
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_AcquisitionFailureCreatesNoConnectionAndIsSafe()
    {
        var driver = Driver;
        driver.RequireStrategy();
        using var source = new PasswordQaSource(driver.Credentials) { Fail = true };
        var observed = new PasswordQaRegistry(driver.Registry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await driver.Target(source, observed).OpenAsync());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialResolutionFailed));
        PasswordQaDriver.AssertSafe(error, "qa-sensitive-source-sentinel");
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(observed.Connections, Is.Empty);
        Assert.That(source.Disposed, Is.False);
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_CancellationDuringAcquisitionCreatesNoConnection()
    {
        var driver = Driver;
        driver.RequireStrategy();
        using var source = new PasswordQaSource(driver.Credentials) { Block = true };
        using var cancellation = new CancellationTokenSource();
        var observed = new PasswordQaRegistry(driver.Registry);
        var opening = driver.Target(source, observed).OpenAsync(cancellation.Token).AsTask();
        await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await opening.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(observed.Connections, Is.Empty);
        Assert.That(source.Disposed, Is.False);
    }

    [TestCase(false), TestCase(true), Category("PasswordAuthentication")]
    public async Task PasswordStrategy_ConfigurationFailureDisposesRealNativeConnection(bool asynchronous)
    {
        var driver = Driver;
        driver.RequireStrategy();
        using var source = new PasswordQaSource(driver.Credentials);
        var observed = new PasswordQaRegistry(driver.Registry);
        var strategy = new FailingConfiguration(ConnectionAuthentication.UsernamePassword(source));
        var target = new ConnectionUrl(driver.WithOptions(false), observed).WithAuthentication(strategy);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        PasswordQaDriver.AssertSafe(error, "qa-sensitive-configuration-sentinel");
        Assert.That(source.Calls, Is.EqualTo(1));
        AssertDisposed(observed, openStarted: false);
        Assert.That(source.Disposed, Is.False);
    }

    [TestCase(false), TestCase(true), Category("PasswordAuthentication")]
    public async Task PasswordStrategy_RejectedPasswordDisposesRealNativeConnection(bool asynchronous)
    {
        var driver = Driver;
        driver.RequirePasswordAuthentication();
        var password = Guid.NewGuid().ToString("N") + "!Aa1";
        using var source = new PasswordQaSource(new(driver.Uri.Username, password));
        var observed = new PasswordQaRegistry(driver.Registry);
        var target = driver.Target(source, observed);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            using var connection = asynchronous ? await target.OpenAsync() : (DbConnection)target.Open();
        });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        PasswordQaDriver.AssertSafe(error, password);
        AssertDisposed(observed, openStarted: true);
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(source.Disposed, Is.False);
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_CancellationDuringNativeOpeningDisposesConnection()
    {
        var driver = Driver;
        driver.RequireStrategy();
        using var native = driver.Registry.GetProviderFactory(driver.Uri.Scheme).CreateConnection()!;
        if (driver.Provider is "Npgsql" or "MySqlConnector" or "MySql.Data.MySqlClient" or "MySql.Data")
            Assert.Ignore("#1689: this native driver's incomplete physical handshake did not honor caller cancellation within the QA bound. Native pool-lease opening cancellation is tested separately; no physical-handshake pass is claimed.");
        if (native.GetType().GetMethod(nameof(DbConnection.OpenAsync), [typeof(CancellationToken)])?.DeclaringType == typeof(DbConnection))
            Assert.Ignore("#1689: this driver uses synchronous DbConnection.OpenAsync fallback; in-flight native opening cancellation cannot be exercised.");
        using var source = new PasswordQaSource(driver.Credentials);
        using var cancellation = new CancellationTokenSource();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = "127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        var observed = new PasswordQaRegistry(driver.Registry);
        var opening = Task.Run(async () => await driver.Target(source, observed, endpoint: endpoint).OpenAsync(cancellation.Token));
        try
        {
            using var client = await listener.AcceptTcpClientAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await opening.WaitAsync(TimeSpan.FromSeconds(10)));
            AssertDisposed(observed, openStarted: true);
            Assert.That(source.Calls, Is.EqualTo(1));
            Assert.That(source.Disposed, Is.False);
        }
        finally
        {
            cancellation.Cancel();
            listener.Stop();
            try { using var connection = await opening.WaitAsync(TimeSpan.FromSeconds(15)); }
            catch (Exception error) when (error is OperationCanceledException or ConnectionAuthenticationException or TimeoutException) { }
        }
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_RotatesDatabasePasswordAcrossActualNativePools()
    {
        var driver = Driver;
        driver.RequirePoolRotation();
        await PasswordQaPoolRotation.VerifyAsync(driver);
    }

    [Test, Category("PasswordAuthentication")]
    public async Task PasswordStrategy_CancellationDuringPooledOpeningDisposesConnection()
    {
        var driver = Driver;
        driver.RequireStrategy();
        if (driver.Provider is not ("Npgsql" or "Microsoft.Data.SqlClient" or "MySqlConnector"))
            Assert.Ignore("#1689: an isolated size-one native pool with cancellable lease waiting is not verified for this QA driver.");
        await PasswordQaPoolCancellation.VerifyAsync(driver);
    }

    private static void AssertDisposed(PasswordQaRegistry observed, bool openStarted)
    {
        Assert.That(observed.Connections, Has.Count.EqualTo(1));
        Assert.That(observed.Connections[0].OpenStarted, Is.EqualTo(openStarted));
        Assert.That(observed.Connections[0].WasDisposed, Is.True, "The native connection must be disposed, not only closed.");
    }

    private sealed class FailingConfiguration(ConnectionAuthentication inner) : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => inner.Kind;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context) => inner.Resolve(context);
        public override ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
            => inner.ResolveAsync(context, cancellationToken);
        public override void Configure(DbConnection connection, AuthenticationContext context)
        { inner.Configure(connection, context); throw new InvalidOperationException("qa-sensitive-configuration-sentinel"); }
        public override async ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, CancellationToken cancellationToken = default)
        { await inner.ConfigureAsync(connection, context, cancellationToken); throw new InvalidOperationException("qa-sensitive-configuration-sentinel"); }
    }
}
