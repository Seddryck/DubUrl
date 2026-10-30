using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Diagnostics;
using System.Text.Json;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using NUnit.Framework;

namespace DubUrl.Provider.MsAccess.QA;

[TestFixture, Category("MsAccess"), NonParallelizable, Platform("Win"), SupportedOSPlatform("windows")]
public sealed class FilePasswordTests
{
    private string directory = null!;
    private string path = null!;
    private string password = null!;
    private ISchemeRegistry registry = null!;

    [OneTimeSetUp]
    public void CreateEncryptedFixture()
    {
        directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DubUrlFilePassword-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        path = System.IO.Path.Combine(directory, "encrypted.accdb");
        password = Guid.NewGuid().ToString("N")[..16] + "A!";
        // Keep classic DAO COM fixture setup outside the .NET OLE DB test host.
        // Send the random password over stdin, never command arguments, a file or output.
        var start = new ProcessStartInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", System.IO.Path.Combine(AppContext.BaseDirectory, "CreateFilePasswordFixture.ps1") })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        process.StandardInput.WriteLine(JsonSerializer.Serialize(new { path, password }));
        process.StandardInput.Close();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("Encrypted Access fixture creation timed out.");
        }
        _ = output.GetAwaiter().GetResult();
        _ = errors.GetAwaiter().GetResult();
        Assert.That(process.ExitCode, Is.Zero, "Could not create encrypted Access fixture; verify installed matching-architecture ACE DAO.");
        DbProviderFactories.RegisterFactory("System.Data.OleDb", OleDbFactory.Instance);
        registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(
            [.. new AssemblyTypesProbe().Assemblies, typeof(OleDbMapper).Assembly])).Build();
    }

    private ConnectionUrl Location()
        => new("oledb+accdb:///" + Uri.EscapeDataString(path) + "?OLE%20DB%20Services=-2", registry);
    private ConnectionUrl Authenticated(string secret)
        => Location().WithAuthentication(ConnectionAuthentication.FilePassword(secret));

    [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
    public async Task EncryptedFileOpensQueriesAndReopens(bool asynchronous, bool openHelper)
    {
        var target = Authenticated(password);
        await using var connection = openHelper
            ? asynchronous ? await target.OpenAsync() : (DbConnection)target.Open()
            : asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        if (!openHelper)
        {
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            if (asynchronous) await connection.OpenAsync(); else connection.Open();
        }
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
        await Query(connection, asynchronous);
        connection.Close();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        if (asynchronous) await connection.OpenAsync(); else connection.Open();
        await Query(connection, asynchronous);
    }

    private static async Task Query(DbConnection connection, bool asynchronous)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [Value] FROM [FilePasswordProbe]";
        var result = asynchronous ? await command.ExecuteScalarAsync() : command.ExecuteScalar();
        Assert.That(Convert.ToInt32(result), Is.EqualTo(1724));
    }

    [TestCase(false), TestCase(true)]
    public async Task WrongPasswordFailsSafelyAndDisposesOwnedConnection(bool asynchronous)
    {
        var strategy = new ObservingStrategy(ConnectionAuthentication.FilePassword("incorrect-placeholder"));
        var target = Location().WithAuthentication(strategy);
        var error = asynchronous
            ? await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.OpenAsync())
            : Assert.Throws<ConnectionAuthenticationException>(() => target.Open());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(error.ToString().Contains(password, StringComparison.Ordinal), Is.False);
        Assert.That(error.ToString(), Does.Not.Contain("incorrect-placeholder"));
        Assert.That(strategy.Connection, Is.Not.Null);
        // A disposed OleDbConnection loses its connection string and cannot be reopened.
        Assert.That(() => strategy.Connection!.Open(), Throws.InstanceOf<InvalidOperationException>());
        Assert.That(strategy.Connection!.ConnectionString, Is.Empty);
    }

    [TestCase(false), TestCase(true)]
    public async Task MissingPasswordCannotReadEncryptedFile(bool asynchronous)
    {
        await using var connection = asynchronous ? await Location().ConnectAsync() : (DbConnection)Location().Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        if (asynchronous) await Assert.ThrowsAsync<OleDbException>(async () => await connection.OpenAsync());
        else Assert.That(() => connection.Open(), Throws.InstanceOf<OleDbException>());
    }

    [OneTimeTearDown]
    public void RemoveFixture()
    {
        // Exercise native error-wrapper cleanup as well as the visible connection operations.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        // Delete only this fixture's known files and non-recursively remove its UUID-owned directory.
        if (directory is null) return;
        File.Delete(path);
        File.Delete(System.IO.Path.ChangeExtension(path, ".laccdb"));
        Directory.Delete(directory);
    }

    private sealed class ObservingStrategy(ConnectionAuthentication inner) : ConnectionAuthentication
    {
        public DbConnection? Connection { get; private set; }
        public override ConnectionAuthenticationKind Kind => inner.Kind;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context) => inner.Resolve(context);
        public override ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
            => inner.ResolveAsync(context, cancellationToken);
        public override void Configure(DbConnection connection, AuthenticationContext context)
        { Connection = connection; inner.Configure(connection, context); }
        public override async ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, CancellationToken cancellationToken = default)
        { Connection = connection; await inner.ConfigureAsync(connection, context, cancellationToken); }
    }
}
