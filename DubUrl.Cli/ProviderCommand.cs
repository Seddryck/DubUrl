using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

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
        return command;
    }

    private static Command CreateList(IServiceProvider services)
    {
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("list", "List curated and installed connectors with activation and health status.");
        command.Options.Add(json);
        command.SetAction(result =>
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
                console.Out.WriteLine("ID\tVERSION\tSOURCE\tACTIVATION\tSTATUS\tALIASES");
                foreach (var connector in connectors)
                    console.Out.WriteLine($"{connector.Id}\t{connector.Version}\t{connector.Source}\t{connector.Activation}\t{connector.Status}\t{string.Join(',', connector.Aliases)}");
            }
            return CliExitCodes.Success;
        });
        return command;
    }

    private static Command CreateInstall(IServiceProvider services, CancellationToken cancellationToken)
    {
        var connector = new Argument<string>("connector") { Description = "Canonical connector ID or alias (for example postgresql)." };
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("install", "Download, verify, install, and activate a curated connector pack for this user.");
        command.Arguments.Add(connector);
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, async () =>
        {
            var requested = result.GetValue(connector)!;
            var installed = await services.GetRequiredService<IProviderPackService>()
                .InstallAsync(requested, cancellationToken)
                .ConfigureAwait(false);
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
            var requested = result.GetValue(connector)!;
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
            services.GetRequiredService<ICliConsole>().Error.WriteLine($"{exception.Code}: {exception.Message}");
            return exception.ExitCode;
        }
    }
}
