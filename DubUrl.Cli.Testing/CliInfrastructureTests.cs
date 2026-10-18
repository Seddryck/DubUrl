using System.Diagnostics;
using DubUrl.Cli;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CliInfrastructureTests
{
    [Test]
    public void Assembly_DoesNotReferenceCompatibilityProvidersOrDatabaseDrivers()
    {
        var references = typeof(CliHost).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(references, Does.Not.Contain("DubUrl"));
            Assert.That(references, Has.None.StartsWith("DubUrl.Provider."));
            Assert.That(references, Does.Not.Contain("Npgsql"));
            Assert.That(references, Does.Not.Contain("Microsoft.Data.SqlClient"));
            Assert.That(references, Does.Not.Contain("Microsoft.Data.Sqlite"));
            Assert.That(references, Does.Not.Contain("MySqlConnector"));
        });
    }

    [Test]
    public async Task Process_VersionAndHelp_AreAvailable()
    {
        var assembly = typeof(CliHost).Assembly.Location;
        var version = await RunProcessAsync(assembly, "--version");
        var help = await RunProcessAsync(assembly, "--help");
        var connectorHelp = await RunProcessAsync(assembly, "connector --help");

        Assert.Multiple(() =>
        {
            Assert.That(version.ExitCode, Is.Zero);
            Assert.That(version.StandardOutput, Does.Match(@"\d+\.\d+\.\d+"));
            Assert.That(help.ExitCode, Is.Zero);
            Assert.That(help.StandardOutput, Does.Contain("schema"));
            Assert.That(help.StandardOutput, Does.Contain("capabilities"));
            Assert.That(help.StandardOutput, Does.Contain("connector"));
            Assert.That(connectorHelp.ExitCode, Is.Zero);
            Assert.That(connectorHelp.StandardOutput, Does.Contain("not database servers"));
            Assert.That(connectorHelp.StandardOutput, Does.Contain("install"));
            Assert.That(connectorHelp.StandardOutput, Does.Contain("uninstall"));
        });
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunProcessAsync(
        string assembly,
        string argument)
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{assembly}\" {argument}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "0";
        startInfo.Environment["COR_ENABLE_PROFILING"] = "0";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the CLI process.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, output, error);
    }
}
