using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class OperatingSystemAuthenticationTests
{
    [TestCase("Npgsql", "timescale")]
    [TestCase("Npgsql", "questdb")]
    [TestCase("Npgsql", "cockroach")]
    [TestCase("MySqlConnector", "mysql")]
    public async Task UnsupportedFamilyRejectsBeforeNativeCreation(string provider, string scheme)
    {
        var context = new AuthenticationContext(provider, scheme, "localhost", null, "app", null);
        var strategy = ConnectionAuthentication.OperatingSystemIdentity();
        Assert.That(() => strategy.Resolve(context), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await strategy.ResolveAsync(context));
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
    }
    [TestCase("mssql://localhost/app")]
    [TestCase("mssql://localhost/app?Integrated%20Security=SSPI")]
    [TestCase("mssql://localhost/app?Trusted_Connection=true")]
    public void SqlServerUsesCurrentSecurityContext(string url)
    {
        using var connection = (SqlConnection)new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity()).Connect();
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
        Assert.That(builder.IntegratedSecurity, Is.True);
        Assert.That(builder.UserID, Is.Empty);
        Assert.That(builder.Password, Is.Empty);
    }
    [TestCase("mssql://alice:@localhost/app")]
    [TestCase("mssql://localhost/app?Integrated%20Security=false")]
    [TestCase("mssql://localhost/app?Authentication=Active%20Directory%20Integrated")]
    [TestCase("postgres://alice@localhost/app")]
    public void RejectsConflictsAndTcpPeer(string url)
        => Assert.That(() => new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity()).Connect(),
            Throws.TypeOf<ConnectionAuthenticationException>());

    [Test]
    public void SocketRepresentationPreservesPathAndPort()
    {
        var target = new ConnectionUrl("postgres://alice@localhost:55432/app?Host=%2Fvar%2Frun%2Fpostgresql", Registry())
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        if (OperatingSystem.IsWindows())
            Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("UnsupportedAuthentication"));
        else
        {
            using var connection = target.Connect();
            var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            Assert.That(builder.Host, Is.EqualTo("/var/run/postgresql"));
            Assert.That(builder.Port, Is.EqualTo(55432));
            Assert.That(builder.Username, Is.EqualTo("alice"));
        }
    }

    [Test, Explicit("Creates and removes a uniquely named fixture-owned LocalDB instance."), Platform("Win")]
    public async Task LocalDbAuthenticatesCurrentUser()
    {
        var name = "DubUrlAuthFixture" + Guid.NewGuid().ToString("N");
        await RunLocalDb("create", name);
        try
        {
            await RunLocalDb("start", name);
            var url = "mssql://localhost/master?Data%20Source=" + Uri.EscapeDataString("(localdb)\\" + name) + "&Pooling=false";
            await using var connection = await new ConnectionUrl(url, Registry()).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity()).OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN ORIGINAL_LOGIN() = SUSER_SNAME() THEN 1 ELSE 0 END";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
        }
        finally
        {
            await RunLocalDb("stop", name);
            await RunLocalDb("delete", name);
        }
    }

    [TestCase("alice", true), TestCase("mapped_alice", true), TestCase("bob", false)]
    [Explicit("Run inside the controlled Linux peer fixture."), Platform("Linux")]
    public async Task UnixSocketPeerAuthenticatesMappedRole(string role, bool succeeds)
    {
        var url = Environment.GetEnvironmentVariable("DUBURL_PEER_URL");
        if (string.IsNullOrEmpty(url)) Assert.Ignore("Missing Linux peer fixture URL.");
        var target = new ConnectionUrl(url!.Replace("postgres://alice@", "postgres://" + role + "@", StringComparison.Ordinal), Registry())
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        if (!succeeds)
        {
            await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => { await using var failed = await target.OpenAsync(); });
            return;
        }
        await using var connection = await target.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT current_user";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(role));
    }

    private static async Task RunLocalDb(string action, string name)
    {
        var start = new System.Diagnostics.ProcessStartInfo("SqlLocalDB.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(action); start.ArgumentList.Add(name);
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await output; _ = await errors;
        Assert.That(process.ExitCode, Is.Zero, "Fixture-owned LocalDB operation failed; verify prerequisites.");
    }
    private static ISchemeRegistry Registry()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        return new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }
}
