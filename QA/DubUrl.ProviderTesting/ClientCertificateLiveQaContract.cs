using System.Data.Common;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

public abstract partial class ClientCertificateAuthenticationQaContract
{
    private async Task<CertificateQaPostgresFixture> RequireCertificateServerAsync()
    {
        var driver = CertificateDriver;
        driver.RequireSupported();
        if (driver.Driver.Provider != "Npgsql" || driver.Driver.Family != "postgresql")
            Assert.Ignore("#1699: this QA has no isolated certificate-required account, trusted TLS server and negative-certificate fixture. CockroachDB is insecure; MySQL/MariaDB/SingleStore are ordinary password fixtures. Native hook passes are not live certificate login. Existing opt-in ClientCertificateLiveTests remain available for externally provisioned targets.");
        return postgresFixture ??= await CertificateQaPostgresFixture.CreateAsync();
    }
    private ConnectionUrl CertificateTarget(CertificateQaPostgresFixture fixture, CertificateQaScope scope, X509Certificate2 certificate, bool wrongHost = false, bool wrongCa = false)
        => new ConnectionUrl(fixture.Url(wrongHost, wrongCa), scope.Registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate));

    [TestCase(false), TestCase(true), Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveCertificateAuthenticatesWithoutPassword(bool asynchronous)
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry);
        var target = CertificateTarget(fixture, scope, fixture.Certificates.Client);
        await using (var connection = asynchronous ? await target.OpenAsync() : (DbConnection)target.Open())
        {
            await PasswordQaDriver.ProbeAsync(connection, "SELECT 1");
            Assert.That(await ServerSerialAsync(connection), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Client)));
            var settings = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            Assert.That(!settings.TryGetValue("Password", out var password) || string.IsNullOrEmpty(password.ToString()), Is.True);
        }
        Assert.That(fixture.Certificates.Client.HasPrivateKey, Is.True);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveMissingCertificateCannotFallback()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry);
        Exception? failure = null;
        try { await using var connection = await new ConnectionUrl(fixture.Url(), scope.Registry).OpenAsync(); }
        catch (Exception error) { failure = error; }
        Assert.That(failure is not null, Is.True, "The cert-only host rule must reject a missing certificate. Native diagnostics are suppressed.");
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveUntrustedClientIsRejectedAndDisposed()
    {
        var fixture = await RequireCertificateServerAsync();
        await AssertRejectedCertificateAsync(fixture, fixture.Certificates.Untrusted);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveWrongIdentityIsRejectedAndDisposed()
    {
        var fixture = await RequireCertificateServerAsync();
        await AssertRejectedCertificateAsync(fixture, fixture.Certificates.WrongIdentity);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveExpiredClientIsRejectedAndDisposed()
    {
        var fixture = await RequireCertificateServerAsync();
        await AssertRejectedCertificateAsync(fixture, fixture.Certificates.Expired);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveUntrustedServerCaIsRejectedAndDisposed()
    {
        var fixture = await RequireCertificateServerAsync();
        await AssertRejectedCertificateAsync(fixture, fixture.Certificates.Client, wrongCa: true);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveWrongServerHostnameIsRejectedAndDisposed()
    {
        var fixture = await RequireCertificateServerAsync();
        await AssertRejectedCertificateAsync(fixture, fixture.Certificates.Client, wrongHost: true);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LivePlaintextCannotFallback()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry);
        Exception? failure = null;
        try { await using var connection = await new ConnectionUrl(fixture.Url(plaintext: true), scope.Registry).OpenAsync(); }
        catch (Exception error) { failure = error; }
        Assert.That(failure is not null, Is.True, "The fixture must reject plaintext rather than taking a weaker authentication rule; diagnostics are suppressed.");
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LivePoolReusesAuthenticatedPhysicalSession()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry, pooling: true);
        var target = CertificateTarget(fixture, scope, fixture.Certificates.Client);
        int session;
        await using (var first = await target.OpenAsync())
        {
            session = await SessionAsync(first);
            Assert.That(await ServerSerialAsync(first), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Client)));
        }
        await using var second = await target.OpenAsync();
        Assert.That(await SessionAsync(second), Is.EqualTo(session));
        Assert.That(await ServerSerialAsync(second), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Client)));
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveRotationClearsOwnedPoolAndUsesReplacement()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry, pooling: true);
        int oldSession;
        await using (var previous = await CertificateTarget(fixture, scope, fixture.Certificates.Client).OpenAsync())
            oldSession = await SessionAsync(previous);
        scope.ClearPools();
        var target = CertificateTarget(fixture, scope, fixture.Certificates.Replacement);
        int replacementSession;
        await using (var replacement = await target.OpenAsync())
        {
            replacementSession = await SessionAsync(replacement);
            Assert.That(replacementSession, Is.Not.EqualTo(oldSession));
            Assert.That(await ServerSerialAsync(replacement), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Replacement)));
        }
        await using var reused = await target.OpenAsync();
        Assert.That(await SessionAsync(reused), Is.EqualTo(replacementSession));
        Assert.That(await ServerSerialAsync(reused), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Replacement)));
        Assert.That(fixture.Certificates.Client.HasPrivateKey && fixture.Certificates.Replacement.HasPrivateKey, Is.True);
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LivePoolWaitCancellationDisposesPendingConnection()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry, pooling: true);
        var target = CertificateTarget(fixture, scope, fixture.Certificates.Client);
        await using var held = await target.OpenAsync();
        using var cancellation = new CancellationTokenSource();
        var pending = target.OpenAsync(cancellation.Token).AsTask();
        Assert.That(pending.IsCompleted, Is.False, "The certificate-authenticated size-one native pool must be exhausted.");
        cancellation.Cancel();
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => { await using var connection = await pending.WaitAsync(TimeSpan.FromSeconds(10)); });
        Assert.That(scope.Registry.Connections, Has.Count.EqualTo(2));
        await CertificateQaDriver.AssertDisposedAsync(scope.Registry.Connections[1]);
        await PasswordQaDriver.ProbeAsync(held, "SELECT 1");
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveClosedConnectionReopensWithCertificate()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry);
        await using var connection = await CertificateTarget(fixture, scope, fixture.Certificates.Client).OpenAsync();
        var original = await SessionAsync(connection);
        await connection.CloseAsync();
        await connection.OpenAsync();
        Assert.That(await SessionAsync(connection), Is.Not.EqualTo(original));
        Assert.That(await ServerSerialAsync(connection), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Client)));
    }

    [Test, Category("ClientCertificateAuthentication")]
    public async Task CertificateQa_LiveCachedSessionDoesNotValidateReplacementIdentity()
    {
        var fixture = await RequireCertificateServerAsync();
        await using var scope = new CertificateQaScope(AuthenticationRegistry, pooling: true);
        int session;
        await using (var first = await CertificateTarget(fixture, scope, fixture.Certificates.Client).OpenAsync()) session = await SessionAsync(first);
        var wrongIdentity = CertificateTarget(fixture, scope, fixture.Certificates.WrongIdentity);
        await using (var reused = await wrongIdentity.OpenAsync())
        {
            Assert.That(await SessionAsync(reused), Is.EqualTo(session));
            Assert.That(await ServerSerialAsync(reused), Is.EqualTo(CertificateQaPostgresFixture.Serial(fixture.Certificates.Client)), "A reused session still has its old client identity; this is not acceptance of the replacement certificate.");
        }
        scope.ClearPools();
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await wrongIdentity.OpenAsync(); });
        AssertSafe(error!);
        await CertificateQaDriver.AssertDisposedAsync(scope.Registry.Connections[^1]);
    }

    private async Task AssertRejectedCertificateAsync(CertificateQaPostgresFixture fixture, X509Certificate2 rejected, bool wrongHost = false, bool wrongCa = false)
    {
        await using var scope = new CertificateQaScope(AuthenticationRegistry);
        var target = CertificateTarget(fixture, scope, rejected, wrongHost, wrongCa);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var connection = await target.OpenAsync(); });
        AssertSafe(error!);
        Assert.That(scope.Registry.Connections, Has.Count.EqualTo(1));
        await CertificateQaDriver.AssertDisposedAsync(scope.Registry.Connections[0]);
        Assert.That(rejected.HasPrivateKey, Is.True);
    }
    private static async Task<int> SessionAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_backend_pid()";
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
    private static async Task<string> ServerSerialAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT client_serial::text FROM pg_stat_ssl WHERE pid=pg_backend_pid() AND ssl";
        return (string)(await command.ExecuteScalarAsync() ?? throw new AssertionException("The authenticated session must expose a TLS client certificate serial."));
    }
}
