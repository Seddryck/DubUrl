using System.ComponentModel;
using System.Diagnostics;

namespace DubUrl.Locating.OdbcDriver;

internal record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);

internal interface ICommandRunner
{
    CommandResult Run(string fileName, params string[] arguments);
}

internal sealed class CommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Unable to start '{fileName}'.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, standardOutput, standardError);
    }
}

internal sealed class UnixOdbcDriverListingStrategy : IDriverListingStrategy
{
    private ICommandRunner Runner { get; }

    public UnixOdbcDriverListingStrategy()
        : this(new CommandRunner()) { }

    internal UnixOdbcDriverListingStrategy(ICommandRunner runner)
        => Runner = runner;

    public string[] List()
    {
        CommandResult result;
        try
        {
            result = Runner.Run("odbcinst", "-q", "-d");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new DriverDiscoveryException("unixODBC driver discovery requires the 'odbcinst' executable.");
        }

        if (result.ExitCode != 0)
            throw new DriverDiscoveryException(
                $"unixODBC driver discovery failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");

        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            .Select(line => line[1..^1].Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
