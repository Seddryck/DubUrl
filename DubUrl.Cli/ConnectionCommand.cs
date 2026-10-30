using System.CommandLine;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Authentication;
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

    private sealed record CheckInvocation(string? Url, string? Name, string? Environment, ConnectionCheckOptions Options, string? Format);
    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var group = new Command("connection", "Check database connections and access.");
        var check = new Command("check", "Open a database and optionally execute one engine or zero-row table check.");
        var url = new Argument<string?>("url") { Arity = ArgumentArity.ZeroOrOne };
        var name = new Option<string?>("--connection") { Description = "Named connection from configuration." };
        var environment = new Option<string?>("--connection-env") { Description = "Environment variable holding the URL." };
        var engine = new Option<bool>("--engine") { Description = "Execute SELECT 1 (or the documented native equivalent)." };
        var table = new Option<string?>("--table") { Description = "Execute a zero-row SELECT against this qualified identifier." };
        var timeout = new Option<int>("--timeout") { DefaultValueFactory = _ => 15, Description = "Overall database-check budget in seconds (default 15)." };
        var format = new Option<string?>("--output-format") { Description = "human or json." };
        var readOnly = ReadOnlyOption();
        check.Options.Add(readOnly);
        var authentication = new CliAuthentication();
        authentication.AddTo(check);
        check.Arguments.Add(url);
        check.Options.Add(name);
        check.Options.Add(environment);
        check.Options.Add(engine);
        check.Options.Add(table);
        check.Options.Add(timeout);
        check.Options.Add(format);
        check.SetAction((result, actionToken) => ExecuteAsync(services, new CheckInvocation(result.GetValue(url), result.GetValue(name), result.GetValue(environment),
            new() { Engine = result.GetValue(engine), Table = result.GetValue(table), TimeoutSeconds = result.GetValue(timeout) },
            result.GetValue(format)), authentication, result, result.GetValue(readOnly), actionToken));
        group.Subcommands.Add(check);
        return group;
    }

    private static async Task<int> ExecuteAsync(IServiceProvider services, CheckInvocation request,
        CliAuthentication authentication, System.CommandLine.ParseResult parsed, bool readOnly, CancellationToken cancellationToken)
    {
        using var authenticationLifetime = authentication;
        var console = services.GetRequiredService<ICliConsole>();
        try
        {
            if (request.Format is not (null or "human" or "json")) throw new ArgumentException("Output format must be human or json.");
            var url = ResolveTarget(services, request.Url, request.Name, request.Environment);
            var target = authentication.Configure(services, parsed, url);
            if (readOnly) target = target.WithReadOnly();
            var options = request.Options with { AuthenticationTimeoutSeconds = authentication.AcquisitionTimeoutSeconds };
            var operation = await services.GetRequiredService<IConnectionCheckService>().CheckAsync(target, options, cancellationToken).ConfigureAwait(false);
            Write(console, operation, request.Format == "json");
            if (operation.Success && readOnly && request.Format != "json") console.Out.WriteLine("Connection opened with native read-only database access.");
            return GetExitCode(operation);
        }
        catch (OperationCanceledException)
        {
            await console.Error.WriteLineAsync("Operation cancelled.").ConfigureAwait(false);
            return CliExitCodes.Cancelled;
        }
        catch (ConnectionAuthenticationException error)
        {
            Write(console, new(false, GetMode(request.Options), null, 0, [new(error.Code, error.Message)]), request.Format == "json");
            return CliAuthentication.ExitCode(error);
        }
        catch (CliOAuthInputException error)
        {
            Write(console, new(false, GetMode(request.Options), null, 0, [new("invalidInput", error.Message)]), request.Format == "json");
            return CliExitCodes.InvalidInput;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or DubUrlException)
        {
            // Do not emit parser/provider exception messages: they may contain URL components or paths.
            Write(console, InvalidResult(request.Options), request.Format == "json");
            return CliExitCodes.InvalidInput;
        }
    }
    private static int GetExitCode(ConnectionCheckResult result)
    {
        if (result.Success) return CliExitCodes.Success;
        return result.Diagnostics.Any(d => d.Code is AuthenticationDiagnosticCodes.UnsupportedAuthentication
            or AuthenticationDiagnosticCodes.CredentialConflict or AuthenticationDiagnosticCodes.MissingCredentials
            or AuthenticationDiagnosticCodes.SynchronousCredentialsRequired)
            ? CliExitCodes.InvalidInput : CliExitCodes.ConnectionCheckFailed;
    }

    private static string ResolveTarget(IServiceProvider services, string? url, string? name, string? environment)
    {
        if (new[] { url, name, environment }.Count(value => value is not null) != 1)
            throw new ArgumentException("Specify exactly one URL, --connection or --connection-env.");
        url ??= environment is not null ? Environment.GetEnvironmentVariable(environment)
            : services.GetRequiredService<IConfiguration>()[$"ConnectionStrings:{name}"];
        return !string.IsNullOrWhiteSpace(url) ? url : throw new ArgumentException("The selected connection is not configured.");
    }
    private static ConnectionCheckResult InvalidResult(ConnectionCheckOptions options)
        => new(false, GetMode(options), null, 0,
            [new("invalidInput", "Invalid connection, selector, identifier, timeout, output format or unsupported SQL mode.")]);
    private static string GetMode(ConnectionCheckOptions options)
    {
        if (options.Table is not null) return "table";
        return options.Engine ? "engine" : "connection";
    }

    private static void Write(ICliConsole console, ConnectionCheckResult result, bool json)
    {
        if (json)
            console.Out.WriteLine(JsonSerializer.Serialize(new { command = "connection check", result.Success, result.Mode,
                result.Provider, result.ElapsedMilliseconds, result.Diagnostics }, CliJson.Options));
        else if (result.Success) console.Out.WriteLine($"Connection check succeeded ({result.Mode}).");
        else foreach (var diagnostic in result.Diagnostics) console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }
}
