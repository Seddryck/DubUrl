using DubUrl.Schema;
using DubUrl.Schema.Documents;
using System.Data.Common;

namespace DubUrl.Application;

/// <summary>Coordinates validation, mapping, rendering, and live database deployment.</summary>
public sealed class SchemaDeploymentService : ISchemaDeploymentService
{
    private readonly SchemaOperationPipeline pipeline;
    private readonly ISchemaScriptDeploymentExecutor executor;

    public SchemaDeploymentService(
        ISchemaDocumentValidator validator,
        ISchemaDocumentMapper mapper,
        ISchemaDomainRenderer renderer,
        ISchemaScriptDeploymentExecutor executor)
    {
        pipeline = new SchemaOperationPipeline(validator, mapper, renderer);
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<SchemaDeploymentResult> DeployAsync(
        ISchemaDocument document,
        ConnectionUrl connection,
        SchemaDeploymentOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(connection);
        cancellationToken.ThrowIfCancellationRequested();

        var dialect = connection.Dialect;
        var dialectName = dialect.Aliases.FirstOrDefault() ?? dialect.GetType().Name;
        var render = pipeline.Render(document, dialect, dialectName,
            options?.CreationOptions ?? SchemaCreationOptions.None);
        if (!render.IsSuccess)
            return new SchemaDeploymentResult(false, document.Version, dialectName, null, render.Diagnostics);

        try
        {
            await executor.ExecuteAsync(connection, render.Sql!, cancellationToken).ConfigureAwait(false);
            return new SchemaDeploymentResult(true, document.Version, dialectName, render.Sql, render.Diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DubUrl.Authentication.ConnectionAuthenticationException error)
        {
            return new SchemaDeploymentResult(false, document.Version, dialectName, render.Sql,
                render.Diagnostics.Append(SchemaOperationPipeline.Error(error.Code, error.Message, "$")).ToArray());
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException or DubUrl.Authentication.ConnectionAuthenticationException)
        {
            return new SchemaDeploymentResult(false, document.Version, dialectName, render.Sql,
                render.Diagnostics
                    .Append(SchemaOperationPipeline.Error(
                        SchemaApplicationDiagnosticCodes.DeploymentFailed, "Database deployment failed. Check authentication and provider configuration.", "$"))
                    .ToArray());
        }
    }
}
