using System.Data.Common;
using DubUrl.Querying.Dialects;
using DubUrl.Schema;
using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Application;

/// <summary>Default adapters for the existing DubUrl dialect, renderer, and connection APIs.</summary>
public sealed class SchemaDialectResolver(IDialectRegistry dialects) : ISchemaDialectResolver
{
    private readonly IDialectRegistry dialects = dialects ?? throw new ArgumentNullException(nameof(dialects));

    public IDialect Resolve(string dialect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dialect);
        return dialects.Get(dialect);
    }
}

/// <summary>Uses the DubUrl schema renderer to produce a dialect-specific script.</summary>
public sealed class SchemaDomainRenderer : ISchemaDomainRenderer
{
    public string Render(RuntimeSchema schema, IDialect dialect, SchemaCreationOptions options)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(dialect);
        return new SchemaScriptRenderer(dialect, options).Render(schema);
    }
}

/// <summary>Executes scripts asynchronously when supported by the selected ADO.NET provider.</summary>
public sealed class SchemaScriptDeploymentExecutor : ISchemaScriptDeploymentExecutor
{
    public async Task ExecuteAsync(
        ConnectionUrl connection,
        string script,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(script);
        cancellationToken.ThrowIfCancellationRequested();

        await using var databaseConnection = await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = databaseConnection.CreateCommand();
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
