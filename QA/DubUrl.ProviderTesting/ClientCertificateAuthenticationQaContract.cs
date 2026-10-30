using System.Data;
using System.Data.Common;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[NonParallelizable]
public abstract partial class ClientCertificateAuthenticationQaContract : OperatingSystemIdentityQaContract
{
    private X509Certificate2? certificate;
    private CertificateQaPostgresFixture? postgresFixture;
    private CertificateQaDriver CertificateDriver => new(new(AuthenticationConnectionUrl, AuthenticationRegistry));
    private X509Certificate2 Certificate => certificate ??= TokenQaCertificates.Create();

    [OneTimeTearDown]
    public async Task DisposeCertificateQaResources()
    {
        try { if (postgresFixture is not null) await postgresFixture.DisposeAsync(); }
        finally { certificate?.Dispose(); }
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_AvailabilityIsExplicit()
    {
        var driver = CertificateDriver;
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        if (driver.Supported)
        {
            await using var connection = await driver.Target(Certificate, registry).ConnectAsync();
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            await CertificateQaDriver.AssertHookAsync(connection, Certificate);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry).ConnectAsync(); });
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
            Assert.That(registry.Connections, Is.Empty);
            AssertSafe(error);
        }
        Assert.That(Certificate.HasPrivateKey, Is.True);
    }

    [TestCase(false), TestCase(true), Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_ConnectIsLazyAndCallerOwned(bool asynchronous)
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        var target = driver.Target(Certificate, new(AuthenticationRegistry));
        _ = target.Parse();
        _ = target.Dialect;
        await using (var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect())
        {
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(target.Parse().Contains(TokenQaSource.Sentinel, StringComparison.Ordinal), Is.False);
        }
        Assert.That(Certificate.HasPrivateKey, Is.True, "Native disposal must not dispose the caller certificate.");
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_NativeHookSelectsOnlyCallerCertificate()
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        await using var connection = await driver.Target(Certificate, new(AuthenticationRegistry)).ConnectAsync();
        await CertificateQaDriver.AssertHookAsync(connection, Certificate);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_UnsupportedSupplementIsRejectedBeforeAcquisition()
    {
        var driver = CertificateDriver;
        if (driver.Supplemental) Assert.Ignore("#1699: this native adapter supports explicit supplemental credentials; the rejection case applies to the other adapters.");
        using var source = new PasswordQaSource(new("qa", TokenQaSource.Sentinel));
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, source).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        Assert.That(source.Calls, Is.Zero);
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [TestCase(false), TestCase(true), Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_SupplementIsExplicitLazyAndResolvedOnce(bool asynchronous)
    {
        var driver = CertificateDriver;
        driver.RequireSupplemental();
        using var source = new PasswordQaSource(driver.Driver.Credentials);
        var target = driver.Target(Certificate, new(AuthenticationRegistry), source);
        _ = target.Parse();
        _ = target.Dialect;
        Assert.That(source.Calls, Is.Zero);
        await using (var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect())
        {
            var settings = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            Assert.That(settings["Password"].ToString() == driver.Driver.Uri.Password, Is.True, "The resolved secret must not be printed.");
            Assert.That(source.Calls, Is.EqualTo(1));
            Assert.That(target.Parse().Contains(driver.Driver.Uri.Password, StringComparison.Ordinal), Is.False);
            await CertificateQaDriver.AssertHookAsync(connection, Certificate);
        }
        Assert.That(source.Disposed, Is.False);
        Assert.That(Certificate.HasPrivateKey, Is.True);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_SupplementFailureCreatesNoConnection()
    {
        var driver = CertificateDriver;
        driver.RequireSupplemental();
        using var source = new PasswordQaSource(driver.Driver.Credentials) { Fail = true };
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, source).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialResolutionFailed));
        Assert.That(registry.Connections, Is.Empty);
        Assert.That(source.Disposed, Is.False);
        PasswordQaDriver.AssertSafe(error, "qa-sensitive-source-sentinel");
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_CancelSupplementAcquisitionCreatesNoConnection()
    {
        var driver = CertificateDriver;
        driver.RequireSupplemental();
        using var source = new PasswordQaSource(driver.Driver.Credentials) { Block = true };
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        using var cancellation = new CancellationTokenSource();
        var connecting = Task.Run(async () => await driver.Target(Certificate, registry, source).ConnectAsync(cancellation.Token));
        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => { await using var connection = await connecting.WaitAsync(TimeSpan.FromSeconds(10)); });
            Assert.That(registry.Connections, Is.Empty);
            Assert.That(source.Disposed, Is.False);
        }
        finally { cancellation.Cancel(); }
    }

    [Test, Category("ClientCertificateAuthentication")]
    public void CertificateQa_AsyncOnlySupplementRejectsSyncBeforeCreation()
    {
        var driver = CertificateDriver;
        driver.RequireSupplemental();
        var source = new AsyncOnlyCertificateQaSource();
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = Assert.Throws<ConnectionAuthenticationException>(() => driver.Target(Certificate, registry, source).Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired));
        Assert.That(source.Calls, Is.Zero);
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_SupplementUsernameMismatchCreatesNoConnection()
    {
        var driver = CertificateDriver;
        driver.RequireSupplemental();
        using var source = new PasswordQaSource(new("qa-mismatched", TokenQaSource.Sentinel));
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, source).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_MissingPrivateKeyRejectsBeforeCreation()
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        using var publicOnly = X509Certificate2.CreateFromPem(Certificate.ExportCertificatePem());
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(publicOnly, registry).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.InvalidCertificate));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_DisposedCertificateRejectsBeforeCreation()
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        using var disposed = TokenQaCertificates.Create();
        disposed.Dispose();
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(disposed, registry).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.InvalidCertificate));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [TestCase("disabled"), TestCase("required"), TestCase("verify-ca"), Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_WeakTlsRejectsBeforeNativeConnectionCreation(string mode)
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, location: driver.Location(weakMode: mode)).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        Assert.That(Certificate.HasPrivateKey, Is.True);
        AssertSafe(error);
    }

    [TestCase("Password"), TestCase("PWD"), TestCase("SSL Certificate"), TestCase("SSL Client Key"), TestCase("Certificate Password"), TestCase("Client Certificate"), Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_CompetingSourceRejectsBeforeCreation(string option)
    {
        var driver = CertificateDriver;
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, location: driver.Location(conflict: option)).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_UrlPasswordRejectsBeforeCreation()
    {
        var driver = CertificateDriver;
        if (driver.Driver.Family is "sqlite" or "duckdb" or "access" or "excel" or "text")
            Assert.Ignore("#1699: file-URL grammar rejects userinfo before certificate conflict validation; this is not a strategy-conflict pass.");
        var location = driver.Driver.Location;
        var index = location.IndexOf("://", StringComparison.Ordinal) + 3;
        location = location[..index] + "qa:" + Uri.EscapeDataString(TokenQaSource.Sentinel) + "@" + location[index..];
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await driver.Target(Certificate, registry, location: location).ConnectAsync(); });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(registry.Connections, Is.Empty);
        AssertSafe(error);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_PreCancelledConnectDoesNotCreateConnection()
    {
        var driver = CertificateDriver;
        var registry = new TokenQaRegistry(AuthenticationRegistry);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => { await using var connection = await driver.Target(Certificate, registry).ConnectAsync(cancellation.Token); });
        Assert.That(registry.Connections, Is.Empty);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_MariaDbAliasConfiguresNativeHookWithoutOpening()
    {
        var driver = CertificateDriver;
        if (driver.Driver.Provider != "MySqlConnector" || driver.Driver.Family != "mysql")
            Assert.Ignore("#1699: the MariaDB native-alias check is carried by the MySqlConnector QA; there is no separate native MariaDB database QA fixture.");
        var location = driver.Location();
        location = "mariadb" + location[location.IndexOf("://", StringComparison.Ordinal)..];
        await using var connection = await driver.Target(Certificate, new(AuthenticationRegistry), location: location).ConnectAsync();
        await CertificateQaDriver.AssertHookAsync(connection, Certificate);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }
    private static void AssertSafe(ConnectionAuthenticationException error) => PasswordQaDriver.AssertSafe(error, TokenQaSource.Sentinel);
    private sealed class AsyncOnlyCertificateQaSource : IUsernamePasswordCredentialSource
    {
        internal int Calls { get; private set; }
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; return new(new UsernamePasswordCredentials("qa", TokenQaSource.Sentinel)); }
    }
}
