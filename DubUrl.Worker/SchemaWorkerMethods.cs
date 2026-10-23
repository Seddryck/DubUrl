using DubUrl.Application;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Worker;

internal static partial class SchemaWorkerMethods
{
    public static void Register(WorkerMethodRegistry registry, IServiceProvider services)
    {
        var capabilities = services.GetRequiredService<SchemaWorkerCapabilities>();
        registry.SetFeature("schema", capabilities);
        RegisterInspection(registry, services);
        registry.Register<SchemaValidateParameters, SchemaValidationResponse>(
            "schema.validate",
            "Validate a versioned schema document without opening a database connection.",
            (parameters, cancellationToken) => ValidateAsync(services, parameters, cancellationToken));
        registry.Register<SchemaRenderParameters, SchemaRenderResponse>(
            "schema.render",
            "Validate and render deterministic dialect-specific SQL.",
            (parameters, cancellationToken) => RenderAsync(services, parameters, cancellationToken));
        registry.Register<SchemaDeployParameters, SchemaDeploymentResponse>(
            "schema.deploy",
            "Render or deploy a schema using a configured connection policy.",
            (parameters, cancellationToken) => DeployAsync(services, parameters, cancellationToken));
    }

    private static ValueTask<SchemaValidationResponse> ValidateAsync(
        IServiceProvider services,
        SchemaValidateParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = RequireDocument(parameters.Document);
        var result = services.GetRequiredService<ISchemaDocumentService>().Validate(document);
        return ValueTask.FromResult(new SchemaValidationResponse(
            result.IsSuccess, result.DocumentVersion, result.Diagnostics));
    }

    private static ValueTask<SchemaRenderResponse> RenderAsync(
        IServiceProvider services,
        SchemaRenderParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = RequireDocument(parameters.Document);
        if (string.IsNullOrWhiteSpace(parameters.Dialect))
            throw new WorkerInvalidParametersException("A dialect is required.");

        var result = services.GetRequiredService<ISchemaDocumentService>().Render(
            document,
            parameters.Dialect,
            new SchemaRenderOptions { CreationOptions = CreationOptions(parameters.DropExisting) });
        return ValueTask.FromResult(ToResponse(result));
    }

    private static async ValueTask<SchemaDeploymentResponse> DeployAsync(
        IServiceProvider services,
        SchemaDeployParameters parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = RequireDocument(parameters.Document);
        var policy = services.GetRequiredService<SchemaWorkerPolicy>();

        var policyFailure = EnforcePolicy(document, parameters, policy);
        if (policyFailure is not null)
            return policyFailure;

        ValidateConnectionSelector(parameters);
        if (!string.IsNullOrWhiteSpace(parameters.ConnectionUrl) && !policy.AllowDirectConnections)
            return Failure(document, SchemaWorkerDiagnosticCodes.DirectConnectionDisabled,
                "Direct connection URLs are disabled by worker policy.", parameters.DryRun);

        var options = CreationOptions(parameters.DropExisting);
        if (!string.IsNullOrWhiteSpace(parameters.Dialect))
            return RenderDryRun(services, document, parameters.Dialect, options);

        var connectionValue = ResolveConnection(services, parameters, document, out var failure);
        if (failure is not null)
            return failure;

        if (!TryCreateConnection(connectionValue!, out var connection))
            return ConnectionInvalid(document, parameters.DryRun);

        if (parameters.DryRun)
            return RenderConnectionDryRun(services, document, connection!, options);

        var deployed = await DeployLiveAsync(services, document, connection!, options, cancellationToken)
            .ConfigureAwait(false);
        return new SchemaDeploymentResponse(
            deployed.IsSuccess,
            deployed.DocumentVersion,
            deployed.Dialect,
            deployed.Sql,
            false,
            Redact(deployed.Diagnostics, connectionValue!));
    }

    private static SchemaDeploymentResponse? EnforcePolicy(
        ISchemaDocument document,
        SchemaDeployParameters parameters,
        SchemaWorkerPolicy policy)
    {
        if (parameters.DropExisting && !policy.AllowDestructive)
            return Failure(document, SchemaWorkerDiagnosticCodes.DestructiveDeploymentDisabled,
                "Destructive schema deployment is disabled by worker policy.", parameters.DryRun);
        if (!parameters.DryRun && !policy.DeploymentEnabled)
            return Failure(document, SchemaWorkerDiagnosticCodes.DeploymentDisabled,
                "Live schema deployment is disabled by worker policy.", false);
        return null;
    }

    private static void ValidateConnectionSelector(SchemaDeployParameters parameters)
    {
        var selectors = CountSelectors(parameters);
        if (selectors != 1 || (!parameters.DryRun && !string.IsNullOrWhiteSpace(parameters.Dialect)))
            throw new WorkerInvalidParametersException(parameters.DryRun
                ? "Specify exactly one of connection, connectionUrl, or dialect."
                : "Specify exactly one of connection or connectionUrl for live deployment.");
    }

    private static SchemaDeploymentResponse RenderDryRun(
        IServiceProvider services,
        ISchemaDocument document,
        string dialect,
        SchemaCreationOptions options)
    {
        var rendered = services.GetRequiredService<ISchemaDocumentService>().Render(
            document, dialect, new SchemaRenderOptions { CreationOptions = options });
        return ToResponse(rendered, true);
    }

    private static SchemaDeploymentResponse RenderConnectionDryRun(
        IServiceProvider services,
        ISchemaDocument document,
        ConnectionUrl connection,
        SchemaCreationOptions options)
    {
        string? dialect;
        try
        {
            dialect = FindDialect(services.GetRequiredService<SchemaWorkerCapabilities>(), connection.Dialect);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ConnectionInvalid(document, true);
        }

        return dialect is null
            ? Failure(document, SchemaWorkerDiagnosticCodes.DialectUnavailable,
                "The selected connection uses a dialect that is unavailable for schema rendering.", true)
            : RenderDryRun(services, document, dialect, options);
    }

    private static Task<SchemaDeploymentResult> DeployLiveAsync(
        IServiceProvider services,
        ISchemaDocument document,
        ConnectionUrl connection,
        SchemaCreationOptions options,
        CancellationToken cancellationToken)
        => services.GetRequiredService<ISchemaDeploymentService>().DeployAsync(
            document,
            connection,
            new SchemaDeploymentOptions { CreationOptions = options },
            cancellationToken);

    private static bool TryCreateConnection(string connectionValue, out ConnectionUrl? connection)
    {
        try
        {
            connection = new ConnectionUrl(connectionValue);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            connection = null;
            return false;
        }
    }

    private static SchemaDeploymentResponse ConnectionInvalid(ISchemaDocument document, bool dryRun)
        => Failure(document, SchemaWorkerDiagnosticCodes.ConnectionInvalid,
            "The selected connection could not be resolved.", dryRun);

    private static string? ResolveConnection(
        IServiceProvider services,
        SchemaDeployParameters parameters,
        ISchemaDocument document,
        out SchemaDeploymentResponse? failure)
    {
        failure = null;
        if (!string.IsNullOrWhiteSpace(parameters.ConnectionUrl))
            return parameters.ConnectionUrl;

        var value = services.GetRequiredService<IConfiguration>()["ConnectionStrings:" + parameters.Connection];
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        failure = Failure(document, SchemaWorkerDiagnosticCodes.ConnectionNotFound,
            "The named connection was not found in worker configuration.", parameters.DryRun);
        return null;
    }

    private static SchemaDeploymentResponse ToResponse(SchemaRenderResult result, bool dryRun)
        => new(result.IsSuccess, result.DocumentVersion, result.Dialect, result.Sql, dryRun, result.Diagnostics);

    private static SchemaRenderResponse ToResponse(SchemaRenderResult result)
        => new(result.IsSuccess, result.DocumentVersion, result.Dialect, result.Sql, result.Diagnostics);

    private static SchemaDeploymentResponse Failure(
        ISchemaDocument document,
        string code,
        string message,
        bool dryRun)
        => new(false, document.Version, string.Empty, null, dryRun,
            [new SchemaOperationDiagnostic(code, SchemaDocumentDiagnosticSeverity.Error, message, "$")]);

    internal static IReadOnlyList<SchemaOperationDiagnostic> Redact(
        IReadOnlyList<SchemaOperationDiagnostic> diagnostics,
        string secret)
    {
        var sensitiveValues = new HashSet<string>(StringComparer.Ordinal) { secret };
        if (Uri.TryCreate(secret, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.UserInfo))
        {
            sensitiveValues.Add(uri.UserInfo);
            foreach (var part in uri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries))
            {
                sensitiveValues.Add(part);
                sensitiveValues.Add(Uri.UnescapeDataString(part));
            }
        }

        return diagnostics.Select(item => item with
        {
            Message = sensitiveValues
                .Where(value => !string.IsNullOrEmpty(value))
                .OrderByDescending(value => value.Length)
                .Aggregate(item.Message,
                    (message, value) => message.Replace(value, "[redacted]", StringComparison.Ordinal))
        }).ToArray();
    }

    private static string? FindDialect(SchemaWorkerCapabilities capabilities, IDialect dialect)
        => dialect.Aliases.FirstOrDefault(alias => capabilities.Dialects.Contains(alias, StringComparer.OrdinalIgnoreCase));

    private static int CountSelectors(SchemaDeployParameters parameters)
        => (string.IsNullOrWhiteSpace(parameters.Connection) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(parameters.ConnectionUrl) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(parameters.Dialect) ? 0 : 1);

    private static SchemaCreationOptions CreationOptions(bool dropExisting)
        => dropExisting ? SchemaCreationOptions.DropIfExists : SchemaCreationOptions.None;

    private static ISchemaDocument RequireDocument(ISchemaDocument? document)
        => document ?? throw new WorkerInvalidParametersException("A schema document is required.");
}

public static class SchemaWorkerDiagnosticCodes
{
    public const string DeploymentDisabled = "SDW001";
    public const string ConnectionNotFound = "SDW002";
    public const string DirectConnectionDisabled = "SDW003";
    public const string DestructiveDeploymentDisabled = "SDW004";
    public const string ConnectionInvalid = "SDW005";
    public const string DialectUnavailable = "SDW006";
}
