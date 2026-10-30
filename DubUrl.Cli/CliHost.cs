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

        var root = args.Length >= 1 && args[0] == "catalog"
            ? CliRootCommandFactory.CreateCatalogPublisher(services, cancellationToken)
            : CliRootCommandFactory.Create(services, cancellationToken);
        var parseResult = root.Parse(args.Length == 0 ? ["--help"] : args);
        if (parseResult.Errors.Count > 0 && args.Length >= 2 && args[0] == "catalog" && args[1] == "create")
        {
            const string message = "Invalid catalog creation arguments. Run 'duburl catalog create --help' for usage; supplied values are not echoed.";
            var console = services.GetRequiredService<ICliConsole>();
            if (args.Contains("--json")) console.Out.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                command = "catalog create", success = false,
                diagnostics = new[] { new { code = "catalog-arguments-invalid", message } },
            }, CliJson.Options));
            else console.Error.WriteLine(message);
            return CliExitCodes.InvalidInput;
        }
        if (parseResult.Errors.Count > 0 && args.Any(argument => CliConnectionOptions.Names.Contains(argument.Split('=')[0])))
        {
            await services.GetRequiredService<ICliConsole>().Error.WriteLineAsync("Invalid connection configuration options. Use --help for supported flags and duration syntax; supplied values are not echoed.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        if (parseResult.Errors.Count > 0 && args.Any(argument => argument.StartsWith("--oauth", StringComparison.Ordinal) || argument == "--allow-oauth-interaction"))
        {
            services.GetRequiredService<ICliConsole>().Error.WriteLine("Invalid OAuth command options. Use --help for supported options; supplied values are not echoed.");
            return CliExitCodes.InvalidInput;
        }
        // The URL selector can contain credentials. Parser diagnostics include rejected
        // tokens verbatim, so use a credential-free message for this command.
        if (parseResult.Errors.Count > 0 && args.Length >= 2 && args[0] == "connection" && args[1] == "check")
        {
            services.GetRequiredService<ICliConsole>().Error.WriteLine("Invalid connection check arguments. Run 'duburl connection check --help' for usage.");
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
