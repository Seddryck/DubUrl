using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class CliRootCommandFactory
{
    public static RootCommand Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var console = services.GetRequiredService<ICliConsole>();
        var root = new RootCommand("Check database connections; inspect, validate, render, and deploy schemas.");
        root.Subcommands.Add(SchemaCommand.Create(services, cancellationToken));
        root.Subcommands.Add(ConnectionCommand.Create(services, cancellationToken));
        root.Subcommands.Add(CredentialCommand.Create(services, cancellationToken));
        root.Subcommands.Add(ConnectorCommand.Create(services, cancellationToken));
        root.Subcommands.Add(CreateCapabilitiesCommand(services));
        root.SetAction(_ =>
        {
            console.Error.WriteLine("A command is required. Run 'duburl --help' for usage.");
            return CliExitCodes.InvalidInput;
        });
        return root;
    }

    private static Command CreateCapabilitiesCommand(IServiceProvider services)
    {
        var command = new Command("capabilities", "Report supported schema and deployment capabilities.");
        var json = new Option<bool>("--json") { Description = "Write a stable JSON document." };
        command.Options.Add(json);
        command.SetAction(result =>
        {
            var console = services.GetRequiredService<ICliConsole>();
            var capabilities = services.GetRequiredService<CliCapabilities>();
            if (result.GetValue(json))
            {
                console.Out.WriteLine(JsonSerializer.Serialize(capabilities, CliJson.Options));
            }
            else
            {
                console.Out.WriteLine($"Schema document versions: {string.Join(", ", capabilities.SchemaDocumentVersions)}");
                console.Out.WriteLine($"Connection commands: {string.Join(", ", capabilities.ConnectionCommands)}");
                console.Out.WriteLine($"Credential commands: {string.Join(", ", capabilities.CredentialCommands)}");
                console.Out.WriteLine($"Connection check modes: {string.Join(", ", capabilities.ConnectionCheckModes)}");
                console.Out.WriteLine($"Schema commands: {string.Join(", ", capabilities.SchemaCommands)}");
                console.Out.WriteLine($"Dialects: {string.Join(", ", capabilities.Dialects)}");
                console.Out.WriteLine($"Providers: {string.Join(", ", capabilities.Providers)}");
                console.Out.WriteLine($"Deployable schemes: {string.Join(", ", capabilities.DeployableSchemes)}");
                console.Out.WriteLine($"Discoverable schemes: {string.Join(", ", capabilities.DiscoverableSchemes)}");
                console.Out.WriteLine($"Deployment available: {capabilities.DeploymentAvailable.ToString().ToLowerInvariant()}");
            }
            return CliExitCodes.Success;
        });
        return command;
    }

    internal static string GetVersion()
    {
        var assembly = typeof(CliRootCommandFactory).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
