using System.Data.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal sealed class IdentityQaConnectionScope : IAsyncDisposable
{
    internal TokenQaRegistry Registry { get; }
    internal IdentityQaConnectionScope(ISchemeRegistry inner, bool pooling)
    {
        Registry = new(inner);
        Registry.Options["Pooling"] = pooling;
        Registry.Options["Application Name"] = "DubUrlIdentityQa_" + Guid.NewGuid().ToString("N");
        Registry.Options["Max Pool Size"] = 1;
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in Registry.Connections)
        {
            var type = connection.GetType();
            var clear = type.GetMethod("ClearPool", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [type], null);
            Assert.That(clear, Is.Not.Null, "#1700: targeted native pool cleanup is required.");
            clear!.Invoke(null, [connection]);
            await connection.DisposeAsync();
        }
    }
}

internal static class IdentityQaProcess
{
    internal static async Task<(int ExitCode, string Output)> RunAsync(string executable, params string[] arguments)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new AssertionException("#1700: fixture process could not start.");
        var output = process.StandardOutput.ReadToEndAsync(budget.Token);
        var errors = process.StandardError.ReadToEndAsync(budget.Token);
        try
        {
            await process.WaitForExitAsync(budget.Token);
            var result = await output;
            _ = await errors;
            return (process.ExitCode, result);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    internal static void Require(bool condition, string operation)
        => Assert.That(condition, Is.True, "#1700: failed to " + operation + "; child output and identity metadata are suppressed.");
}

internal sealed class IdentityQaLocalDbFixture : IAsyncDisposable
{
    private readonly string name = "DubUrlIdentityQa_" + Guid.NewGuid().ToString("N");
    private bool created;
    internal string Url => Location(name);
    internal static string Location(string name) => "mssql://localhost/master?Data%20Source=" + Uri.EscapeDataString("(localdb)\\" + name) + "&Connect%20Timeout=5";
    internal static async Task<IdentityQaLocalDbFixture> CreateAsync()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("#1700: LocalDB requires Windows. Existing SQL Server Actions QA runs on Linux without Kerberos; CI is unchanged and SQL-password container login is not integrated-authentication proof.");
        try
        {
            var available = await IdentityQaProcess.RunAsync("SqlLocalDB.exe", "versions");
            if (available.ExitCode != 0) Assert.Ignore("#1700: LocalDB prerequisite is unavailable; no installation or default-instance mutation is performed.");
        }
        catch (System.ComponentModel.Win32Exception) { Assert.Ignore("#1700: SqlLocalDB.exe is missing; install prerequisites independently. QA does not change machine configuration."); }
        var fixture = new IdentityQaLocalDbFixture();
        try
        {
            await fixture.RunAsync("create");
            fixture.created = true;
            await fixture.RunAsync("start");
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }
    private async Task RunAsync(string action)
        => IdentityQaProcess.Require((await IdentityQaProcess.RunAsync("SqlLocalDB.exe", action, name)).ExitCode == 0, action + " its UUID-owned LocalDB instance");
    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        try { await RunAsync("stop"); }
        finally { await RunAsync("delete"); }
        created = false;
    }
}

/// <summary>Runs the same native QA cases inside the existing PostgreSQL image with the already-installed SDK and compiled test bundle.</summary>
internal sealed class IdentityQaPeerFixture : IAsyncDisposable
{
    private const string Image = "postgres:17@sha256:2d2b8998d31037bf721cfdf764d76ba74171b4fab3431b7f72c27c56ddbdf9e3";
    private readonly string container = "duburl-identity-qa-" + Guid.NewGuid().ToString("N");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "duburl-identity-qa-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> outcomes = [];
    private bool launched;
    internal static async Task<IdentityQaPeerFixture> CreateAsync(string assembly)
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("#1700: automatic controlled peer QA requires the existing Linux PostgreSQL Docker runner.");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" && Environment.GetEnvironmentVariable("DUBURL_IDENTITY_QA_PEER_CONTAINER") != "1")
            Assert.Ignore("#1700: local peer-container QA is opt-in with DUBURL_IDENTITY_QA_PEER_CONTAINER=1 on an already provisioned Linux host; it is run in Actions by existing QA.");
        var fixture = new IdentityQaPeerFixture();
        try { await fixture.StartAsync(assembly); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }
    private async Task StartAsync(string assembly)
    {
        var image = await IdentityQaProcess.RunAsync("docker", "image", "inspect", "--format", "{{.Id}}", Image);
        IdentityQaProcess.Require(image.ExitCode == 0 && image.Output.Trim().StartsWith("sha256:", StringComparison.Ordinal), "reuse the existing pinned PostgreSQL image");
        var runtime = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory()).Parent!.Parent!.Parent!.FullName;
        var console = Directory.GetFiles(Path.Combine(runtime, "sdk"), "vstest.console.dll", SearchOption.AllDirectories)
            .OrderByDescending(path => path, StringComparer.Ordinal).First();
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        var script = Path.Combine(directory, "run.sh");
        await File.WriteAllTextAsync(script, Bootstrap.Replace("\r\n", "\n", StringComparison.Ordinal));
        // Only generated TRX is writable by the fixture OS user; SDK and compiled QA remain read-only.
        var results = Path.Combine(directory, "results");
        Directory.CreateDirectory(results);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(results, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        var qaDirectory = Path.GetDirectoryName(assembly)!;
        var started = await IdentityQaProcess.RunAsync("docker", "run", "--detach", "--name", container,
            "--network", "none", "--mount", "type=bind,src=" + runtime + ",dst=/dotnet,readonly",
            "--mount", "type=bind,src=" + qaDirectory + ",dst=/qa,readonly",
            "--mount", "type=bind,src=" + directory + ",dst=/fixture",
            "--env", "DOTNET_ROOT=/dotnet", "--env", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
            "--env", "DUBURL_PEER_QA_CHILD=1", "--entrypoint", "bash", image.Output.Trim(), "/fixture/run.sh",
            "/dotnet/" + Path.GetRelativePath(runtime, console), "/qa/" + Path.GetFileName(assembly));
        launched = started.ExitCode == 0;
        IdentityQaProcess.Require(launched, "start its network-isolated peer QA container");
        var waited = await IdentityQaProcess.RunAsync("docker", "wait", container);
        IdentityQaProcess.Require(waited.ExitCode == 0, "wait for the bounded peer QA child");
        var report = Path.Combine(results, "peer.trx");
        IdentityQaProcess.Require(File.Exists(report), "produce actual peer child TRX (missing SDK/native prerequisites are a failure, not a pass)");
        var xml = XDocument.Load(report);
        for (var root = new DirectoryInfo(qaDirectory); root is not null; root = root.Parent)
        {
            if (!File.Exists(Path.Combine(root.FullName, "DubUrl.sln"))) continue;
            var archive = Path.Combine(root.FullName, ".test-results", "postgresql");
            Directory.CreateDirectory(archive);
            File.Copy(report, Path.Combine(archive, "identity-peer-net" + Environment.Version.Major + ".0.trx"), overwrite: true);
            break;
        }
        foreach (var result in xml.Descendants().Where(element => element.Name.LocalName == "UnitTestResult"))
        {
            var name = (string?)result.Attribute("testName");
            if (name is not null && name.StartsWith("OsIdentityQa_LivePeer", StringComparison.Ordinal))
                outcomes.Add(name, (string?)result.Attribute("outcome") ?? "Missing");
        }
        IdentityQaProcess.Require(outcomes.Count == 10, "record all ten actual peer scenarios exactly once");
    }
    internal void AssertPassed(string name)
    {
        Assert.That(outcomes.TryGetValue(name, out var outcome), Is.True, "#1700: named peer child case missing from TRX.");
        Assert.That(outcome, Is.EqualTo("Passed"), "#1700: controlled container child must pass the named scenario; a green unrelated process is not proof.");
    }
    public async ValueTask DisposeAsync()
    {
        var removed = !launched;
        if (launched) removed = (await IdentityQaProcess.RunAsync("docker", "rm", "--force", "--volumes", container)).ExitCode == 0;
        var path = Path.GetFullPath(directory);
        var safe = Path.GetDirectoryName(path) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
            && Path.GetFileName(path).StartsWith("duburl-identity-qa-", StringComparison.Ordinal);
        if (safe && Directory.Exists(path)) Directory.Delete(path, recursive: true);
        IdentityQaProcess.Require(removed && safe, "clean up only its UUID-owned container, volumes and temporary directory");
    }
    private const string Bootstrap = """
        #!/usr/bin/env bash
        set -euo pipefail
        export PATH="/dotnet:$PATH"
        export PGDATA=/tmp/duburl-identity-data
        mkdir -p "$PGDATA" /tmp/duburl-identity-socket
        chown postgres:postgres "$PGDATA" /tmp/duburl-identity-socket
        useradd --create-home alice
        gosu postgres initdb --auth-local=peer --auth-host=reject >/dev/null
        gosu postgres pg_ctl -o "-k /tmp/duburl-identity-socket -p 55432 -h ''" -w start >/dev/null
        trap 'gosu postgres pg_ctl -m immediate stop >/dev/null' EXIT
        gosu postgres psql -h /tmp/duburl-identity-socket -p 55432 -v ON_ERROR_STOP=1 <<'SQL'
        CREATE ROLE alice LOGIN;
        CREATE ROLE mapped_alice LOGIN;
        CREATE ROLE bob LOGIN;
        CREATE DATABASE app OWNER alice;
        SQL
        printf 'local all all peer map=fixture\nhost all all all reject\n' > "$PGDATA/pg_hba.conf"
        printf 'fixture alice alice\nfixture alice mapped_alice\nfixture postgres postgres\n' > "$PGDATA/pg_ident.conf"
        gosu postgres pg_ctl reload >/dev/null
        gosu alice env HOME=/home/alice DOTNET_CLI_HOME=/home/alice /dotnet/dotnet "$1" "$2" \
          '/TestCaseFilter:FullyQualifiedName~OsIdentityQa_LivePeer&FullyQualifiedName~PostgreSqlProviderTests' \
          '/Logger:trx;LogFileName=peer.trx' /ResultsDirectory:/fixture/results
        """;
}
