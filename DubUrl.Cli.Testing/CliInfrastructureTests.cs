using System.Diagnostics;
using DubUrl.Cli;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CliInfrastructureTests
{
    [Test]
    public async Task Process_VersionAndHelp_AreAvailable()
    {
        var assembly = typeof(CliHost).Assembly.Location;
        var version = await RunProcessAsync(assembly, "--version");
        var help = await RunProcessAsync(assembly, "--help");
        var providerHelp = await RunProcessAsync(assembly, "provider --help");

        Assert.Multiple(() =>
        {
            Assert.That(version.ExitCode, Is.Zero);
            Assert.That(version.StandardOutput, Does.Match(@"\d+\.\d+\.\d+"));
            Assert.That(help.ExitCode, Is.Zero);
            Assert.That(help.StandardOutput, Does.Contain("schema"));
            Assert.That(help.StandardOutput, Does.Contain("capabilities"));
            Assert.That(help.StandardOutput, Does.Contain("provider"));
            Assert.That(providerHelp.ExitCode, Is.Zero);
            Assert.That(providerHelp.StandardOutput, Does.Contain("not database servers"));
            Assert.That(providerHelp.StandardOutput, Does.Contain("add"));
            Assert.That(providerHelp.StandardOutput, Does.Contain("remove"));
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
