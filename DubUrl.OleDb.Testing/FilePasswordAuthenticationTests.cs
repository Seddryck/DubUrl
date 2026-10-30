using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Runtime.Versioning;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using NUnit.Framework;

namespace DubUrl.OleDb.Testing;

[TestFixture, NonParallelizable]
public class FilePasswordAuthenticationTests
{
    private const string Location = "oledb+accdb:///not-created.accdb?Provider=Microsoft.ACE.OLEDB.12.0";
    private static ISchemeRegistry Registry()
    {
        if (OperatingSystem.IsWindows()) DbProviderFactories.RegisterFactory("System.Data.OleDb", OleDbFactory.Instance);
        return new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(
            [.. new AssemblyTypesProbe().Assemblies, typeof(OleDbMapper).Assembly])).Build();
    }
    private static ConnectionUrl Target(IFileSecretCredentialSource source, string url = Location)
        => new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.FilePassword(source));

    [Test, Platform("Win"), SupportedOSPlatform("windows")]
    public async Task ResolvesOncePerClosedConnectionAndKeepsMetadataSecretFree()
    {
        var source = new Source();
        var target = Target(source);
        target.ValidateAuthentication();
        _ = target.Dialect;
        _ = target.Connectivity;
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
        Assert.That(source.Calls, Is.Zero);
        using var first = target.Connect();
        await using var second = await target.ConnectAsync();
        Assert.That(first, Is.TypeOf<OleDbConnection>());
        Assert.That(first.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(second.State, Is.EqualTo(ConnectionState.Closed));
        var one = new OleDbConnectionStringBuilder(first.ConnectionString);
        var two = new OleDbConnectionStringBuilder(second.ConnectionString);
        Assert.That(one["Jet OLEDB:Database Password"], Is.EqualTo("sentinel1;='@%+"));
        Assert.That(two["Jet OLEDB:Database Password"], Is.EqualTo("sentinel2;='@%+"));
        Assert.That(one.ContainsKey("User ID"), Is.False);
        Assert.That(one.ContainsKey("Password"), Is.False);
        Assert.That(source.Calls, Is.EqualTo(2));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
        Assert.That(target.Url, Is.EqualTo(Location));
        first.Dispose();
        await second.DisposeAsync();
        Assert.That(source.Disposed, Is.False);
    }

    [TestCase("accdb"), TestCase("access"), TestCase("msaccess"), TestCase("mdb")]
    [Platform("Win"), SupportedOSPlatform("windows")]
    public void FilePasswordSupportsAccessAliases(string alias)
    {
        using var connection = new ConnectionUrl($"oledb+{alias}:///absent.accdb?Provider=microsoft.ace.oledb.16.0", Registry())
            .WithAuthentication(ConnectionAuthentication.FilePassword("placeholder")).Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(new OleDbConnectionStringBuilder(connection.ConnectionString)["Jet OLEDB:Database Password"], Is.EqualTo("placeholder"));
    }

    [TestCase("Jet%20OLEDB%3ADatabase%20Password=")]
    [TestCase("Jet%20OLEDB%3ADatabase%20Password=sentinel")]
    [TestCase("Jet%20OLEDB%3ANew%20Database%20Password=sentinel")]
    [TestCase("User%20ID=alice")]
    [TestCase("Password=")]
    [TestCase("Pwd=sentinel")]
    [TestCase("Integrated%20Security=true")]
    [TestCase("Extended%20Properties=Jet%20OLEDB%3ADatabase%20Password%3Dsentinel")]
    [TestCase("ENCRYPTION_KEY=sentinel")]
    [TestCase("Jet%20OLEDB%3ASystem%20Database=workgroup.mdw")]
    public async Task ConflictsRejectBeforeSourceResolution(string option)
    {
        var source = new Source();
        var target = Target(source, Location + "&" + option);
        Assert.That(() => target.ValidateAuthentication(), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.ConnectAsync());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
        Assert.That(source.Calls, Is.Zero);
    }

    [TestCase("oledb+accdb://alice@localhost/absent.accdb")]
    [TestCase("oledb+accdb://alice:@localhost/absent.accdb")]
    public void RejectsUserinfo(string url)
        => Assert.That(() => Target(new Source(), url).ValidateAuthentication(), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));

    [TestCase("System.Data.Odbc", "odbc+accdb")]
    [TestCase("System.Data.OleDb", "oledb+xlsx")]
    [TestCase("System.Data.OleDb", "oledb+mssql")]
    [TestCase("DuckDB.NET.Data", "duckdb")]
    public async Task UnsupportedProvidersRejectBeforeAcquiringSecrets(string provider, string scheme)
    {
        var source = new Source();
        var strategy = ConnectionAuthentication.FilePassword(source);
        var context = new AuthenticationContext(provider, scheme, null, null, "absent", null);
        Assert.That(() => strategy.Resolve(context), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await strategy.ResolveAsync(context));
        Assert.That(source.Calls, Is.Zero);
    }

    [Test, Platform("Win"), SupportedOSPlatform("windows")]
    public async Task AsyncOnlySourceIsNotBlockedBySyncConnect()
    {
        var source = new AsyncSource();
        var target = Target(source);
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired));
        Assert.That(source.Calls, Is.Zero);
        await using var connection = await target.ConnectAsync();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(source.Calls, Is.EqualTo(1));
    }

    [TestCase(false), TestCase(true), Platform("Win")]
    public async Task SourceFailureIsSafe(bool asynchronous)
    {
        var target = Target(new Source { Fail = true });
        var error = asynchronous
            ? await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.ConnectAsync())
            : Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialResolutionFailed));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(error.ToString(), Does.Not.Contain("sentinel"));
    }

    [Test, Platform("Win"), SupportedOSPlatform("windows")]
    public void RejectsUnsupportedNativeProviderBeforeResolution()
    {
        var source = new Source();
        var target = Target(source, "oledb+accdb:///absent.mdb?Provider=Microsoft.Jet.OLEDB.4.0");
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        Assert.That(source.Calls, Is.Zero);
    }

    [TestCase(false), TestCase(true), Platform("Win")]
    public async Task EmptySecretFailsWithoutOpening(bool asynchronous)
    {
        var target = new ConnectionUrl(Location, Registry()).WithAuthentication(ConnectionAuthentication.FilePassword(""));
        var error = asynchronous
            ? await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.ConnectAsync())
            : Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.MissingCredentials));
    }

    [Test]
    public async Task CancellationBeforeConnectDoesNotResolveSource()
    {
        var source = new Source();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Target(source).ConnectAsync(cancellation.Token));
        Assert.That(source.Calls, Is.Zero);
    }

    [Test, Platform("Win"), SupportedOSPlatform("windows")]
    public async Task CancellationDuringResolutionIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new CancellingSource(cancellation);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Target(source).ConnectAsync(cancellation.Token));
    }

    [Test]
    public void PublicSecretObjectsAndStrategyDoNotDisplaySecret()
    {
        Assert.That(new FileSecretCredentials("sentinel").ToString(), Does.Not.Contain("sentinel"));
        Assert.That(ConnectionAuthentication.FilePassword("sentinel").ToString(), Does.Not.Contain("sentinel"));
        Assert.That(ConnectionAuthentication.FilePassword("sentinel").Kind, Is.EqualTo(ConnectionAuthenticationKind.FileSecret));
        Assert.That(() => ConnectionAuthentication.FilePassword((IFileSecretCredentialSource)null!), Throws.ArgumentNullException);
    }

    private sealed class Source : ISynchronousFileSecretCredentialSource, IDisposable
    {
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public bool Fail { get; init; }
        public FileSecretCredentials Resolve(AuthenticationContext context)
        {
            Calls++;
            if (Fail) throw new InvalidOperationException("sentinel");
            return new("sentinel" + Calls + ";='@%+");
        }
        public ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return new(Resolve(context)); }
        public void Dispose() => Disposed = true;
    }

    private sealed class AsyncSource : IFileSecretCredentialSource
    {
        public int Calls { get; private set; }
        public async ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); Calls++; return new("sentinel"); }
    }
    private sealed class CancellingSource(CancellationTokenSource cancellation) : IFileSecretCredentialSource
    {
        public ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellation.Cancel(); cancellationToken.ThrowIfCancellationRequested(); return new(new FileSecretCredentials("sentinel")); }
    }
}
