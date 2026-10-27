using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Owned cert-only PostgreSQL fixture using the image already pulled by existing provider QA.</summary>
internal sealed class CertificateQaPostgresFixture : IAsyncDisposable
{
    private const string Image = "postgres:17@sha256:2d2b8998d31037bf721cfdf764d76ba74171b4fab3431b7f72c27c56ddbdf9e3";
    private readonly string container = "duburl-cert-qa-" + Guid.NewGuid().ToString("N");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "duburl-cert-qa-" + Guid.NewGuid().ToString("N"));
    internal CertificateQaMaterials Certificates { get; } = new();
    private int port;
    private bool started;
    private CertificateQaPostgresFixture() { }

    internal static async Task<CertificateQaPostgresFixture> CreateAsync()
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("#1699: automatic isolated certificate-only PostgreSQL QA requires the existing Linux Docker runner; no Windows/macOS fixture is implemented.");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" && Environment.GetEnvironmentVariable("DUBURL_CERT_QA_POSTGRES_CONTAINER") != "1")
            Assert.Ignore("#1699: local container QA is opt-in. Set DUBURL_CERT_QA_POSTGRES_CONTAINER=1 on an already provisioned Linux Docker host; no local Docker or machine configuration is installed.");
        var fixture = new CertificateQaPostgresFixture();
        try { await fixture.StartAsync(); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }
    private async Task StartAsync()
    {
        var image = await RunAsync(["image", "inspect", "--format", "{{.Id}}", Image]);
        Require(image.Success && image.Output.Trim().StartsWith("sha256:", StringComparison.Ordinal), "reuse the existing pinned PostgreSQL QA image");
        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await WriteAsync("ca.crt", Certificates.Authority.ExportCertificatePem());
        await WriteAsync("other-ca.crt", Certificates.OtherAuthority.ExportCertificatePem());
        await WriteAsync("server.crt", Certificates.Server.ExportCertificatePem());
        using (var key = Certificates.Server.GetRSAPrivateKey())
            await WriteAsync("server.key", key!.ExportRSAPrivateKeyPem());
        await WriteAsync("bootstrap.sh", Bootstrap);
        await WriteAsync("initialize.sh", Initialize);
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(Path.Combine(directory, "initialize.sh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        var launched = await RunAsync(["run", "--detach", "--name", container, "--publish", "127.0.0.1::5432",
            "--env", "POSTGRES_HOST_AUTH_METHOD=reject", "--mount", "type=bind,src=" + directory + ",dst=/fixture,readonly",
            "--mount", "type=bind,src=" + Path.Combine(directory, "initialize.sh") + ",dst=/docker-entrypoint-initdb.d/zz-certificate.sh,readonly",
            "--entrypoint", "bash", image.Output.Trim(), "/fixture/bootstrap.sh", "postgres", "-c", "ssl=on",
            "-c", "ssl_cert_file=/var/lib/postgresql/server.crt", "-c", "ssl_key_file=/var/lib/postgresql/server.key", "-c", "ssl_ca_file=/var/lib/postgresql/ca.crt"]);
        started = launched.Success;
        Require(started, "start its owned certificate-only PostgreSQL container");
        await WaitReadyAsync();
        var published = await RunAsync(["port", container, "5432/tcp"]);
        Require(published.Success && int.TryParse(published.Output.Trim().Split(':')[^1], out port), "resolve the owned loopback-only port");
    }
    private async Task WaitReadyAsync()
    {
        for (var attempt = 0; attempt < 45; attempt++)
        {
            var ready = await RunAsync(["exec", container, "pg_isready", "-h", "127.0.0.1", "-U", "alice", "-d", "app"]);
            if (ready.Success) return;
            await Task.Delay(500);
        }
        Require(false, "reach bounded certificate-only server readiness");
    }
    internal string Url(bool wrongHost = false, bool wrongCa = false, bool plaintext = false)
        => "postgres://alice@" + (wrongHost ? "127.0.0.1" : "localhost") + ":" + port.ToString(CultureInfo.InvariantCulture)
            + "/app?Pooling=false&Timeout=5&SSL%20Mode=" + (plaintext ? "Disable" : "VerifyFull")
            + "&Root%20Certificate=" + Uri.EscapeDataString(Path.Combine(directory, wrongCa ? "other-ca.crt" : "ca.crt"));
    internal static string Serial(X509Certificate2 certificate)
        => BigInteger.Parse("0" + certificate.GetSerialNumberString(), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    private async Task WriteAsync(string name, string contents)
    {
        var path = Path.Combine(directory, name);
        await File.WriteAllTextAsync(path, contents.Replace("\r\n", "\n", StringComparison.Ordinal));
        if (OperatingSystem.IsLinux()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    private static void Require(bool condition, string operation)
        => Assert.That(condition, Is.True, "#1699: unable to " + operation + ". Child diagnostics are suppressed; no keys or credential values are logged.");
    private static async Task<(bool Success, string Output)> RunAsync(string[] arguments)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new AssertionException("#1699: Docker could not start for isolated QA.");
        var output = process.StandardOutput.ReadToEndAsync(budget.Token);
        var error = process.StandardError.ReadToEndAsync(budget.Token);
        try
        {
            await process.WaitForExitAsync(budget.Token);
            var text = await output;
            await error;
            return (process.ExitCode == 0, text);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            var removed = await RunAsync(["rm", "--force", "--volumes", container]);
            if (started) Require(removed.Success, "remove its owned container and anonymous volumes");
        }
        finally
        {
            Certificates.Dispose();
            if (Directory.Exists(directory))
            {
                var parent = Path.GetDirectoryName(Path.GetFullPath(directory));
                if (parent != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(directory).StartsWith("duburl-cert-qa-", StringComparison.Ordinal))
                    throw new AssertionException("The owned certificate fixture directory did not match its expected temporary parent.");
                Directory.Delete(directory, recursive: true);
            }
        }
    }
    private const string Bootstrap = """
        #!/usr/bin/env bash
        set -euo pipefail
        cp /fixture/server.crt /var/lib/postgresql/server.crt
        cp /fixture/server.key /var/lib/postgresql/server.key
        cp /fixture/ca.crt /var/lib/postgresql/ca.crt
        chown postgres:postgres /var/lib/postgresql/server.{crt,key} /var/lib/postgresql/ca.crt
        chmod 600 /var/lib/postgresql/server.key
        export POSTGRES_PASSWORD
        POSTGRES_PASSWORD=$(head -c 32 /dev/urandom | base64)
        exec docker-entrypoint.sh "$@"
        """;
    private const string Initialize = """
        #!/usr/bin/env bash
        set -euo pipefail
        psql --username postgres --dbname postgres -v ON_ERROR_STOP=1 <<'SQL'
        CREATE ROLE alice LOGIN;
        CREATE DATABASE app OWNER alice;
        SQL
        cat > "$PGDATA/pg_hba.conf" <<'HBA'
        local all all peer
        hostssl app alice all cert
        host all all all reject
        HBA
        """;
}
