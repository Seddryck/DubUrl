using System.CommandLine;
using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static partial class SchemaCommand
{
    private static Command CreateInspect(IServiceProvider services, CancellationToken cancellationToken)
    {
        var connection = new Option<string?>("--connection") { Description = "Named connection from configuration. Exactly one connection selector is required." };
        var environment = new Option<string?>("--connection-env") { Description = "Environment variable containing a URL. Exclusive with --connection." };
        var tables = new Option<string[]>("--table")
        {
            Description = "Qualified table name; repeat to select several. Omit to inspect all visible tables.",
            AllowMultipleArgumentsPerToken = false
        };
        var schemas = new Option<string[]>("--schema") { Description = "Schema name; repeat to select several schemas.", AllowMultipleArgumentsPerToken = false };
        var output = new Option<string?>("--output") { Description = "Write the document to a file. Omit for standard output." };
        var format = new Option<string?>("--output-format") { Description = "yaml or json. Overrides the output filename extension; defaults to YAML on stdout. Unknown extensions require this option." };
        var partial = new Option<bool>("--allow-partial") { Description = "Deliberately export incomplete metadata with diagnostics on standard error. Strict export is the default." };
        var references = new Option<bool>("--include-references") { Description = "Include the transitive foreign-key target tables outside the filters." };
        var basic = new Option<bool>("--basic") { Description = "Request tables, columns, primary keys, and foreign keys only." };
        var command = new Command("inspect", "Read database metadata and export a versioned schema document. Does not change the database.");
        var authentication = new CliAuthentication();
        authentication.AddTo(command);
        foreach (var option in new Option[] { connection, environment, tables, schemas, output, format, partial, references, basic }) command.Options.Add(option);
        command.SetAction((result, actionToken) => ExecuteSafelyAsync(services, "inspect", async () =>
        {
            using var authenticationLifetime = authentication;
            actionToken.ThrowIfCancellationRequested();
            var name = result.GetValue(connection);
            var variable = result.GetValue(environment);
            if (string.IsNullOrWhiteSpace(name) == string.IsNullOrWhiteSpace(variable))
                throw new CliInputException("Specify exactly one of --connection or --connection-env.");
            var path = result.GetValue(output);
            var outputFormat = InspectOutputFormat(result.GetValue(format), path);
            var selected = InspectTables(result.GetValue(tables) ?? []);
            var options = new DiscoveryOptions
            {
                Tables = selected,
                Schemas = result.GetValue(schemas) ?? [],
                AllowPartial = result.GetValue(partial),
                IncludeReferencedTables = result.GetValue(references),
                Features = result.GetValue(basic) ? [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys] : Enum.GetValues<SchemaFeature>()
            };
            var value = ResolveConnection(services, name, variable);
            var operation = await services.GetRequiredService<ISchemaInspectionService>().InspectAsync(
                authentication.Configure(services, result, value), options, actionToken).ConfigureAwait(false);
            var console = services.GetRequiredService<ICliConsole>();
            foreach (var diagnostic in operation.Diagnostics)
                console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            if (!operation.IsSuccess) return InspectionFailureExitCode(operation);
            WriteInspection(services, operation, path, outputFormat, actionToken);
            return operation.Inspection.IsComplete ? CliExitCodes.Success : CliExitCodes.InspectionIncomplete;
        }, result.GetValue(format) == "json"));
        return command;
    }

    private static SchemaDocumentFormat InspectOutputFormat(string? format, string? path)
    {
        return format?.ToLowerInvariant() switch
        {
            null => path is null ? SchemaDocumentFormat.Yaml : SchemaDocumentReader.GetFormat(path),
            "yaml" => SchemaDocumentFormat.Yaml,
            "json" => SchemaDocumentFormat.Json,
            _ => throw new CliInputException("--output-format must be 'yaml' or 'json'.")
        };
    }

    private static MetadataName[] InspectTables(string[] tables)
    {
        try { return tables.Select(MetadataNames.Parse).ToArray(); }
        catch (ArgumentException) { throw new CliInputException("A --table value is not a valid qualified identifier."); }
    }

    private static int InspectionFailureExitCode(SchemaExportResult operation)
    {
        if (operation.Diagnostics.Any(diagnostic => diagnostic.Coverage == MetadataCoverage.Unsupported))
            return CliExitCodes.InspectionUnsupported;
        return operation.Inspection.Schema is not null ? CliExitCodes.InspectionIncomplete : CliExitCodes.InspectionFailed;
    }

    private static void WriteInspection(IServiceProvider services, SchemaExportResult operation, string? path,
        SchemaDocumentFormat outputFormat, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        services.GetRequiredService<SchemaDocumentWriter>().Write(memory, operation.Document!, outputFormat);
        if (path is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllBytes(path, memory.ToArray());
        }
        else
        {
            memory.Position = 0;
            using var reader = new StreamReader(memory);
            services.GetRequiredService<ICliConsole>().Out.Write(reader.ReadToEnd());
        }
    }
}
