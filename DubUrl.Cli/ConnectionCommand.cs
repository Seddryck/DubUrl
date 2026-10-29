using System.CommandLine;
using System.Data.Common;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class ConnectionCommand
{
    internal static Option<bool> ReadOnlyOption() => new("--read-only")
    {
        Description = "Require native read-only access to the primary database file. Unsupported routes fail; this is not replica routing or a transaction default."
    };

    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var root = new Command("connection", "Check database connections.");
        var command = new Command("check", "Open and dispose a connection. Does not run write probes or change database settings.");
        var url = new Argument<string?>("url") { Arity = ArgumentArity.ZeroOrOne, Description = "Connection URL; exclusive with named and environment selectors." };
        var name = new Option<string?>("--connection") { Description = "Named connection from configuration." };
        var environment = new Option<string?>("--connection-env") { Description = "Environment variable containing a connection URL." };
        var readOnly = ReadOnlyOption();
        command.Arguments.Add(url);
        command.Options.Add(name);
        command.Options.Add(environment);
        command.Options.Add(readOnly);
        command.SetAction(async result =>
        {
            var console = services.GetRequiredService<ICliConsole>();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var selectors = new[] { result.GetValue(url), result.GetValue(name), result.GetValue(environment) };
                if (selectors.Count(value => !string.IsNullOrWhiteSpace(value)) != 1)
                {
                    console.Error.WriteLine("Specify exactly one URL, --connection, or --connection-env.");
                    return CliExitCodes.InvalidInput;
                }
                var value = selectors[0];
                if (!string.IsNullOrWhiteSpace(selectors[1]))
                    value = services.GetRequiredService<IConfiguration>()["ConnectionStrings:" + selectors[1]];
                if (!string.IsNullOrWhiteSpace(selectors[2])) value = Environment.GetEnvironmentVariable(selectors[2]!);
                if (string.IsNullOrWhiteSpace(value))
                {
                    console.Error.WriteLine("The selected named connection or environment variable is missing or empty.");
                    return CliExitCodes.InvalidInput;
                }
                var target = new ConnectionUrl(value, services.GetRequiredService<ISchemeRegistry>());
                if (result.GetValue(readOnly)) target = target.WithReadOnly();
                using var connection = target.Connect();
                if (connection is DbConnection dbConnection)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    connection.Open();
                }
                cancellationToken.ThrowIfCancellationRequested();
                console.Out.WriteLine(result.GetValue(readOnly)
                    ? "Connection opened with native read-only database access."
                    : "Connection opened successfully.");
                return CliExitCodes.Success;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (NotSupportedException)
            {
                console.Error.WriteLine("The requested option or database mode is unsupported by the selected provider. --read-only requires a verified SQLite or DuckDB file route.");
                return CliExitCodes.InvalidInput;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or DubUrlException)
            {
                console.Error.WriteLine("The connection URL, provider selection, or requested configuration is invalid.");
                return CliExitCodes.InvalidInput;
            }
            catch (Exception)
            {
                // Driver messages may contain credentials or connection strings.
                console.Error.WriteLine("The selected connection could not be opened. Check the provider, file access, and connection configuration.");
                return CliExitCodes.ProviderOperationFailed;
            }
        });
        root.Subcommands.Add(command);
        return root;
    }
}
