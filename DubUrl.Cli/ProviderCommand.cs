using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;

namespace DubUrl.Cli;

internal static class ConnectorCommand
{
    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var command = new Command(
            "connector",
            "Manage client-side connector packs. Connectors are integrations, not database servers.");
        command.Subcommands.Add(CreateList(services));
        command.Subcommands.Add(CreateInstall(services, cancellationToken));
        command.Subcommands.Add(CreateUninstall(services));
        command.Subcommands.Add(CreateMirror(services, cancellationToken));
        return command;
    }

    private static Command CreateMirror(IServiceProvider services, CancellationToken cancellationToken)
    {
        var catalog = new Argument<string>("catalog") { Description = "Administrator-approved upstream catalog file." };
        var output = new Argument<string>("output") { Description = "New directory for the unpublished mirror snapshot." };
        var baseUrl = new Argument<string>("archive-base-url") { Description = "Internal HTTPS URL where the archives will be hosted." };
        var selections = new Argument<string[]>("connectors") { Description = "Pinned selections in connector-id@version form.", Arity = ArgumentArity.OneOrMore };
        var command = new Command("mirror", "Prepare verified archives and a catalog for internal hosting or offline transfer.");
        command.Arguments.Add(catalog);
        command.Arguments.Add(output);
        command.Arguments.Add(baseUrl);
        command.Arguments.Add(selections);
        command.SetAction(result => ExecuteSafelyAsync(services, async () =>
        {
            try
            {
                var pinned = (result.GetValue(selections) ?? []).Select(value =>
                {
                    var parts = value.Split('@');
                    if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace))
                        throw new ConnectorOperationException("mirror-selection-invalid", "Use connector-id@version for every selection.");
                    return new ConnectorMirrorSelection(parts[0], parts[1]);
                }).ToArray();
                if (!Uri.TryCreate(result.GetValue(baseUrl), UriKind.Absolute, out var uri))
                    throw new ConnectorOperationException("mirror-url-invalid", "An absolute internal HTTPS archive URL is required.");
                using var stream = File.OpenRead(result.GetValue(catalog)!);
                var approved = ProviderPackCatalog.Read(stream);
                await new ConnectorMirror(services.GetRequiredService<HttpClient>())
                    .PrepareAsync(approved, pinned, uri, result.GetValue(output)!, cancellationToken).ConfigureAwait(false);
                services.GetRequiredService<ICliConsole>().Out.WriteLine("Verified mirror snapshot prepared. Publish archives before publishing its catalog.");
                return CliExitCodes.Success;
            }
            catch (ConnectorOperationException exception)
            {
                throw new ProviderPackCommandException(exception.Code, exception.Message, CliExitCodes.ProviderOperationFailed);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                throw new ProviderPackCommandException("connector-catalog-invalid", "The approved catalog could not be read or is invalid.", CliExitCodes.ProviderOperationFailed);
            }
        }));
        return command;
    }

    private static Command CreateList(IServiceProvider services)
    {
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("list", "List curated and installed connectors with activation and health status.");
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, () =>
        {
            var console = services.GetRequiredService<ICliConsole>();
            var connectors = services.GetRequiredService<IProviderPackService>().List();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "connector list",
                    connectors,
                }, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine("ID\tVERSION\tSOURCE\tSTATE\tLISTED\tINSTALLED\tACTIVE\tUNHEALTHY\tALIASES");
                foreach (var connector in connectors)
                    console.Out.WriteLine($"{connector.Id}\t{connector.Version}\t{connector.Source}\t{connector.State}\t{connector.Listed}\t{connector.Installed}\t{connector.Active}\t{connector.Unhealthy}\t{string.Join(',', connector.Aliases)}");
            }
            return Task.FromResult(CliExitCodes.Success);
        }));
        return command;
    }

    private static Command CreateInstall(IServiceProvider services, CancellationToken cancellationToken)
    {
        var connector = new Argument<string>("connector") { Description = "Canonical connector ID or alias (for example postgresql)." };
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("install", "Download, verify, install, and activate a curated connector pack for this user.");
        var archive = new Option<string?>("--archive") { Description = "Install this local ZIP against the approved catalog without network access or fallback." };
        command.Arguments.Add(connector);
        command.Options.Add(archive);
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, async () =>
        {
            var requested = result.GetValue(connector)
                ?? throw new InvalidOperationException("A connector ID is required.");
            var service = services.GetRequiredService<IProviderPackService>();
            var installed = result.GetValue(archive) is { } path
                ? await service.InstallOfflineAsync(requested, path, cancellationToken).ConfigureAwait(false)
                : await service.InstallAsync(requested, cancellationToken).ConfigureAwait(false);
            var console = services.GetRequiredService<ICliConsole>();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "connector install",
                    success = true,
                    requestedConnector = requested,
                    connector = installed,
                    activationAppliesTo = "new-processes",
                }, CliJson.Options));
            }
            else
            {
                if (!requested.Equals(installed.Id, StringComparison.OrdinalIgnoreCase))
                    console.Out.WriteLine($"Resolved '{requested}' to connector '{installed.Id}'.");
                console.Out.WriteLine($"Connector '{installed.Id}' {installed.Version} is active for new processes and newly created provider catalogs.");
            }
            return CliExitCodes.Success;
        }));
        return command;
    }

    private static Command CreateUninstall(IServiceProvider services)
    {
        var connector = new Argument<string>("connector") { Description = "Installed connector ID or catalog alias." };
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("uninstall", "Atomically deactivate and remove a user-scoped connector pack.");
        command.Arguments.Add(connector);
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, () =>
        {
            var requested = result.GetValue(connector)
                ?? throw new InvalidOperationException("A connector ID is required.");
            var id = services.GetRequiredService<IProviderPackService>().Uninstall(requested);
            var console = services.GetRequiredService<ICliConsole>();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "connector uninstall",
                    success = true,
                    requestedConnector = requested,
                    connectorId = id,
                    activationAppliesTo = "new-processes",
                }, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine($"Connector '{id}' was uninstalled. Running processes are unchanged.");
            }
            return Task.FromResult(CliExitCodes.Success);
        }));
        return command;
    }

    private static async Task<int> ExecuteSafelyAsync(IServiceProvider services, Func<Task<int>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (ProviderPackCommandException exception)
        {
            await services.GetRequiredService<ICliConsole>().Error
                .WriteLineAsync($"{exception.Code}: {exception.Message}")
                .ConfigureAwait(false);
            return exception.ExitCode;
        }
    }
}
