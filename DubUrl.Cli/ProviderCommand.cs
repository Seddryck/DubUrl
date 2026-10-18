using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class ProviderCommand
{
    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var command = new Command(
            "provider",
            "Manage client-side ADO.NET provider packs. Provider packs are integrations, not database servers.");
        command.Subcommands.Add(CreateList(services));
        command.Subcommands.Add(CreateAdd(services, cancellationToken));
        command.Subcommands.Add(CreateRemove(services));
        return command;
    }

    private static Command CreateList(IServiceProvider services)
    {
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("list", "List bundled and optional provider packs with activation and health status.");
        command.Options.Add(json);
        command.SetAction(result =>
        {
            var console = services.GetRequiredService<ICliConsole>();
            var providers = services.GetRequiredService<IProviderPackService>().List();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "provider list",
                    providers,
                }, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine("NAME\tVERSION\tSOURCE\tACTIVATION\tSTATUS");
                foreach (var provider in providers)
                    console.Out.WriteLine($"{provider.Name}\t{provider.Version}\t{provider.Source}\t{provider.Activation}\t{provider.Status}");
            }
            return CliExitCodes.Success;
        });
        return command;
    }

    private static Command CreateAdd(IServiceProvider services, CancellationToken cancellationToken)
    {
        var provider = new Argument<string>("provider") { Description = "Provider name from the curated catalog (for example duckdb)." };
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("add", "Download, verify, install, and activate a curated provider pack for this user.");
        command.Arguments.Add(provider);
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, async () =>
        {
            var installed = await services.GetRequiredService<IProviderPackService>()
                .AddAsync(result.GetValue(provider)!, cancellationToken)
                .ConfigureAwait(false);
            var console = services.GetRequiredService<ICliConsole>();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "provider add",
                    success = true,
                    provider = installed,
                    activationAppliesTo = "new-processes",
                }, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine($"Provider pack '{installed.Name}' {installed.Version} is active for new processes and newly created provider catalogs.");
            }
            return CliExitCodes.Success;
        }));
        return command;
    }

    private static Command CreateRemove(IServiceProvider services)
    {
        var provider = new Argument<string>("provider") { Description = "Installed optional provider name." };
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        var command = new Command("remove", "Atomically deactivate and remove an optional user-scoped provider pack.");
        command.Arguments.Add(provider);
        command.Options.Add(json);
        command.SetAction(result => ExecuteSafelyAsync(services, () =>
        {
            var name = result.GetValue(provider)!;
            services.GetRequiredService<IProviderPackService>().Remove(name);
            var console = services.GetRequiredService<ICliConsole>();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(new
                {
                    command = "provider remove",
                    success = true,
                    provider = name,
                    activationAppliesTo = "new-processes",
                }, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine($"Optional provider pack '{name}' was removed. Running processes are unchanged.");
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
