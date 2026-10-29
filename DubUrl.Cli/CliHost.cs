using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

public static class CliHost
{
    public static async Task<int> InvokeAsync(
        string[] args,
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(services);

        var root = CliRootCommandFactory.Create(services, cancellationToken);
        var parseResult = root.Parse(args.Length == 0 ? ["--help"] : args);
        if (parseResult.Errors.Count > 0 && args.Any(argument => argument.StartsWith("--oauth", StringComparison.Ordinal) || argument == "--allow-oauth-interaction"))
        {
            services.GetRequiredService<ICliConsole>().Error.WriteLine("Invalid OAuth command options. Use --help for supported options; supplied values are not echoed.");
            return CliExitCodes.InvalidInput;
        }
        try
        {
            var exitCode = await parseResult.InvokeAsync(cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                services.GetRequiredService<ICliConsole>().Error.WriteLine("Operation cancelled.");
                return CliExitCodes.Cancelled;
            }
            return parseResult.Errors.Count > 0 && exitCode != CliExitCodes.Success
                ? CliExitCodes.InvalidInput
                : exitCode;
        }
        catch (OperationCanceledException)
        {
            services.GetRequiredService<ICliConsole>().Error.WriteLine("Operation cancelled.");
            return CliExitCodes.Cancelled;
        }
        catch (Exception)
        {
            services.GetRequiredService<ICliConsole>().Error.WriteLine("Unexpected error. No connection details are reported.");
            return CliExitCodes.UnexpectedError;
        }
    }
}
