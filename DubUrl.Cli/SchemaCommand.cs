using System.CommandLine;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class SchemaCommand
{
    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var command = new Command("schema", "Validate, render, or deploy a versioned schema document.");
        command.Subcommands.Add(CreateValidate(services, cancellationToken));
        command.Subcommands.Add(CreateRender(services, cancellationToken));
        command.Subcommands.Add(CreateDeploy(services, cancellationToken));
        return command;
    }

    private static Command CreateValidate(IServiceProvider services, CancellationToken cancellationToken)
    {
        var input = CreateInputArgument();
        var inputFormat = CreateInputFormatOption();
        var outputFormat = CreateOutputFormatOption();
        var command = new Command("validate", "Validate a schema document without opening a database connection.");
        command.Arguments.Add(input);
        command.Options.Add(inputFormat);
        command.Options.Add(outputFormat);
        command.SetAction(result => ExecuteSafely(services, "validate", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = IsJson(result.GetValue(outputFormat));
            var document = ReadDocument(services, result.GetValue(input)!, result.GetValue(inputFormat));
            var operation = services.GetRequiredService<ISchemaDocumentService>().Validate(document);
            WriteValidationResult(services, operation, json);
            return operation.IsSuccess ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        }));
        return command;
    }

    private static Command CreateRender(IServiceProvider services, CancellationToken cancellationToken)
    {
        var input = CreateInputArgument();
        var inputFormat = CreateInputFormatOption();
        var outputFormat = CreateOutputFormatOption();
        var dialect = new Option<string>("--dialect") { Description = "SQL dialect (for example pgsql, mssql, or sqlite).", Required = true };
        var output = new Option<string?>("--output") { Description = "Write SQL to this file instead of standard output." };
        var dropExisting = new Option<bool>("--drop-existing") { Description = "Render explicit drop-if-exists statements before creation." };
        var command = new Command("render", "Validate and render deterministic dialect-specific SQL.");
        command.Arguments.Add(input);
        command.Options.Add(inputFormat);
        command.Options.Add(outputFormat);
        command.Options.Add(dialect);
        command.Options.Add(output);
        command.Options.Add(dropExisting);
        command.SetAction(result => ExecuteSafely(services, "render", () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = IsJson(result.GetValue(outputFormat));
            var document = ReadDocument(services, result.GetValue(input)!, result.GetValue(inputFormat));
            var options = new SchemaRenderOptions
            {
                CreationOptions = result.GetValue(dropExisting) ? SchemaCreationOptions.DropIfExists : SchemaCreationOptions.None
            };
            var operation = services.GetRequiredService<ISchemaDocumentService>()
                .Render(document, result.GetValue(dialect)!, options);
            var outputPath = result.GetValue(output);
            if (operation.IsSuccess && outputPath is not null)
                WriteOutputFile(outputPath, operation.Sql!);
            WriteRenderResult(services, operation, json, outputPath);
            return operation.IsSuccess ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
        }));
        return command;
    }

    private static Command CreateDeploy(IServiceProvider services, CancellationToken cancellationToken)
    {
        var input = CreateInputArgument();
        var inputFormat = CreateInputFormatOption();
        var outputFormat = CreateOutputFormatOption();
        var connection = new Option<string?>("--connection") { Description = "Named connection from configuration." };
        var connectionEnvironment = new Option<string?>("--connection-env") { Description = "Name of an environment variable containing the connection URL." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Print the exact SQL without opening a connection." };
        var dropExisting = new Option<bool>("--drop-existing") { Description = "Drop existing objects before creation." };
        var yes = new Option<bool>("--yes") { Description = "Confirm destructive deployment in non-interactive use." };
        var command = new Command("deploy", "Validate, render, and execute a schema through the application service.");
        command.Arguments.Add(input);
        command.Options.Add(inputFormat);
        command.Options.Add(outputFormat);
        command.Options.Add(connection);
        command.Options.Add(connectionEnvironment);
        command.Options.Add(dryRun);
        command.Options.Add(dropExisting);
        command.Options.Add(yes);
        command.SetAction(result => ExecuteSafely(services, "deploy", () =>
            ExecuteDeployAsync(
                services,
                result.GetValue(input)!,
                result.GetValue(inputFormat),
                result.GetValue(outputFormat),
                result.GetValue(connection),
                result.GetValue(connectionEnvironment),
                result.GetValue(dryRun),
                result.GetValue(dropExisting),
                result.GetValue(yes),
                cancellationToken).GetAwaiter().GetResult()));
        return command;
    }

    private static async Task<int> ExecuteDeployAsync(
        IServiceProvider services,
        string input,
        string? inputFormat,
        string? outputFormat,
        string? connectionName,
        string? connectionEnvironment,
        bool dryRun,
        bool dropExisting,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = IsJson(outputFormat);
        if (string.IsNullOrWhiteSpace(connectionName) == string.IsNullOrWhiteSpace(connectionEnvironment))
            throw new CliInputException("Specify exactly one of --connection or --connection-env.");

        var connectionUrl = ResolveConnection(services, connectionName, connectionEnvironment);
        var document = ReadDocument(services, input, inputFormat);
        var creationOptions = dropExisting ? SchemaCreationOptions.DropIfExists : SchemaCreationOptions.None;

        if (dryRun)
        {
            try
            {
                var connection = new ConnectionUrl(connectionUrl, services.GetRequiredService<ISchemeRegistry>());
                var dialect = connection.Dialect.Aliases.First();
                var rendered = services.GetRequiredService<ISchemaDocumentService>().Render(document, dialect,
                    new SchemaRenderOptions { CreationOptions = creationOptions });
                WriteRenderResult(services, rendered, json, outputPath: null, command: "deploy", dryRun: true);
                return rendered.IsSuccess ? CliExitCodes.Success : CliExitCodes.ValidationFailed;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new CliInputException($"The selected connection could not be resolved: {Redact(exception.Message, connectionUrl)}");
            }
        }

        if (dropExisting && !confirmed)
        {
            var console = services.GetRequiredService<ICliConsole>();
            if (!console.IsInteractive)
                throw new CliInputException("Destructive non-interactive deployment requires --yes.");
            console.Error.Write("This deployment can drop existing objects. Type 'yes' to continue: ");
            if (!string.Equals(console.In.ReadLine(), "yes", StringComparison.OrdinalIgnoreCase))
                throw new CliInputException("Destructive deployment was not confirmed.");
        }

        SchemaDeploymentResult operation;
        try
        {
            operation = await services.GetRequiredService<ISchemaDeploymentService>().DeployAsync(
                document,
                new ConnectionUrl(connectionUrl, services.GetRequiredService<ISchemeRegistry>()),
                new SchemaDeploymentOptions { CreationOptions = creationOptions },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new CliInputException($"Deployment failed: {Redact(exception.Message, connectionUrl)}");
        }
        operation = operation with
        {
            Diagnostics = operation.Diagnostics
                .Select(item => item with { Message = Redact(item.Message, connectionUrl) })
                .ToArray()
        };
        WriteDeploymentResult(services, operation, json);
        return operation.IsSuccess ? CliExitCodes.Success : CliExitCodes.DeploymentFailed;
    }

    private static int ExecuteSafely(IServiceProvider services, string command, Func<int> action)
    {
        try
        {
            return action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is CliInputException or SchemaDocumentException or IOException or UnauthorizedAccessException)
        {
            var console = services.GetRequiredService<ICliConsole>();
            console.Error.WriteLine($"{command}: {exception.Message}");
            return CliExitCodes.InvalidInput;
        }
    }

    private static ISchemaDocument ReadDocument(IServiceProvider services, string input, string? formatName)
    {
        var format = ParseFormat(input, formatName);
        var reader = services.GetRequiredService<SchemaDocumentReader>();
        if (input != "-")
        {
            using var stream = File.OpenRead(input);
            return reader.Read(stream, format, input);
        }

        var console = services.GetRequiredService<ICliConsole>();
        using var memory = new MemoryStream();
        using (var writer = new StreamWriter(memory, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(console.In.ReadToEnd());
            writer.Flush();
        }
        memory.Position = 0;
        return reader.Read(memory, format, "standard input");
    }

    private static SchemaDocumentFormat ParseFormat(string input, string? formatName)
    {
        if (!string.IsNullOrWhiteSpace(formatName))
            return formatName.ToLowerInvariant() switch
            {
                "json" => SchemaDocumentFormat.Json,
                "yaml" or "yml" => SchemaDocumentFormat.Yaml,
                _ => throw new CliInputException("--input-format must be 'json' or 'yaml'.")
            };
        if (input == "-")
            throw new CliInputException("--input-format is required when reading from standard input.");
        return SchemaDocumentReader.GetFormat(input);
    }

    private static string ResolveConnection(IServiceProvider services, string? name, string? environmentName)
    {
        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            var environmentValue = Environment.GetEnvironmentVariable(environmentName);
            return !string.IsNullOrWhiteSpace(environmentValue)
                ? environmentValue
                : throw new CliInputException($"Environment variable '{environmentName}' is not set.");
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        return configuration[$"ConnectionStrings:{name}"] is { Length: > 0 } configuredValue
            ? configuredValue
            : throw new CliInputException($"Named connection '{name}' was not found in configuration.");
    }

    private static void WriteValidationResult(IServiceProvider services, SchemaValidationResult result, bool json)
    {
        var console = services.GetRequiredService<ICliConsole>();
        if (json)
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "schema validate",
                success = result.IsSuccess,
                documentVersion = result.DocumentVersion,
                diagnostics = result.Diagnostics
            }, CliJson.Options));
            return;
        }

        WriteDiagnostics(console, result.Diagnostics);
        if (result.IsSuccess)
            console.Out.WriteLine($"Schema document version {result.DocumentVersion} is valid.");
    }

    private static void WriteRenderResult(
        IServiceProvider services,
        SchemaRenderResult result,
        bool json,
        string? outputPath,
        string command = "render",
        bool dryRun = false)
    {
        var console = services.GetRequiredService<ICliConsole>();
        if (json)
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = $"schema {command}",
                success = result.IsSuccess,
                documentVersion = result.DocumentVersion,
                result.Dialect,
                sql = outputPath is null ? result.Sql : null,
                output = outputPath,
                dryRun,
                diagnostics = result.Diagnostics
            }, CliJson.Options));
            return;
        }

        WriteDiagnostics(console, result.Diagnostics);
        if (result.IsSuccess && outputPath is null)
            console.Out.Write(result.Sql);
    }

    private static void WriteDeploymentResult(IServiceProvider services, SchemaDeploymentResult result, bool json)
    {
        var console = services.GetRequiredService<ICliConsole>();
        if (json)
        {
            console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                command = "schema deploy",
                success = result.IsSuccess,
                documentVersion = result.DocumentVersion,
                result.Dialect,
                diagnostics = result.Diagnostics
            }, CliJson.Options));
            return;
        }

        WriteDiagnostics(console, result.Diagnostics);
        if (result.IsSuccess)
            console.Out.WriteLine($"Schema document version {result.DocumentVersion} deployed successfully using {result.Dialect}.");
    }

    private static void WriteDiagnostics(ICliConsole console, IReadOnlyList<SchemaOperationDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
            console.Error.WriteLine($"{diagnostic.Severity.ToString().ToLowerInvariant()} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}");
    }

    private static void WriteOutputFile(string path, string sql)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, sql, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static Argument<string> CreateInputArgument()
        => new("input") { Description = "Schema document path, or '-' for standard input." };

    private static Option<string?> CreateInputFormatOption()
        => new("--input-format") { Description = "Input format for standard input or an extensionless file: yaml or json." };

    private static Option<string?> CreateOutputFormatOption()
        => new("--output-format") { Description = "Output format: human (default) or json." };

    private static bool IsJson(string? value)
        => value?.ToLowerInvariant() switch
        {
            null or "human" => false,
            "json" => true,
            _ => throw new CliInputException("--output-format must be 'human' or 'json'.")
        };

    private static string Redact(string message, string secret)
        => string.IsNullOrEmpty(secret) ? message : message.Replace(secret, "[redacted]", StringComparison.Ordinal);

    private sealed class CliInputException(string message) : Exception(message);
}
