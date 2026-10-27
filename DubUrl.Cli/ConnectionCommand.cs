using System.CommandLine;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class ConnectionCommand
{
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
        var authentication = new CliAuthentication();
        authentication.AddTo(check);
        check.Arguments.Add(url);
        check.Options.Add(name);
        check.Options.Add(environment);
        check.Options.Add(engine);
        check.Options.Add(table);
        check.Options.Add(timeout);
        check.Options.Add(format);
        check.SetAction(result => ExecuteAsync(services, result.GetValue(url), result.GetValue(name), result.GetValue(environment),
            new() { Engine = result.GetValue(engine), Table = result.GetValue(table), TimeoutSeconds = result.GetValue(timeout) },
            result.GetValue(format), authentication, result, cancellationToken).GetAwaiter().GetResult());
        group.Subcommands.Add(check);
        return group;
    }

    private static async Task<int> ExecuteAsync(IServiceProvider services, string? url, string? name, string? environment,
        ConnectionCheckOptions options, string? format, CliAuthentication authentication, System.CommandLine.ParseResult parsed, CancellationToken cancellationToken)
    {
        var console = services.GetRequiredService<ICliConsole>();
        try
        {
            if (format is not (null or "human" or "json")) throw new ArgumentException("Output format must be human or json.");
            if (new[] { url, name, environment }.Count(value => value is not null) != 1)
                throw new ArgumentException("Specify exactly one URL, --connection or --connection-env.");
            url ??= environment is not null ? Environment.GetEnvironmentVariable(environment)
                : services.GetRequiredService<IConfiguration>()[$"ConnectionStrings:{name}"];
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("The selected connection is not configured.");
            var operation = await services.GetRequiredService<IConnectionCheckService>().CheckAsync(
                authentication.Configure(services, parsed, url), options, cancellationToken).ConfigureAwait(false);
            Write(console, operation, format == "json");
            return operation.Success ? CliExitCodes.Success : CliExitCodes.ConnectionCheckFailed;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or DubUrlException or DubUrl.Authentication.ConnectionAuthenticationException)
        {
            // Do not emit parser/provider exception messages: they may contain URL components or paths.
            Write(console, new(false, options.Table is not null ? "table" : options.Engine ? "engine" : "connection", null, 0,
                [new("invalidInput", "Invalid connection, selector, identifier, timeout, output format or unsupported SQL mode.")]), format == "json");
            return CliExitCodes.InvalidInput;
        }
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
