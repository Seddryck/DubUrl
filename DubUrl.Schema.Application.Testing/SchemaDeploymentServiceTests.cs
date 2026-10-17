using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;
using NUnit.Framework;

namespace DubUrl.Schema.Application.Testing;

public class SchemaDeploymentServiceTests
{
    [Test]
    public async Task DeployAsync_Success_ReturnsRenderedSql()
    {
        var executor = new RecordingExecutor();
        var service = CreateService(executor);

        var result = await service.DeployAsync(
            SchemaDocumentServiceTests.CreateDocument(),
            new TestConnectionUrl(GetDialect()));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Sql, Does.Contain("CREATE TABLE customer"));
            Assert.That(executor.Script, Is.EqualTo(result.Sql));
        });
    }

    [Test]
    public async Task DeployAsync_ExecutorFailure_ReturnsStableDiagnostic()
    {
        var service = CreateService(new FailingExecutor());

        var result = await service.DeployAsync(
            SchemaDocumentServiceTests.CreateDocument(),
            new TestConnectionUrl(GetDialect()));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics.Single().Code,
                Is.EqualTo(SchemaApplicationDiagnosticCodes.DeploymentFailed));
            Assert.That(result.Sql, Does.Contain("CREATE TABLE customer"));
        });
    }

    [Test]
    public void DeployAsync_Cancellation_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateService(new RecordingExecutor());

        Assert.That(async () => await service.DeployAsync(
                SchemaDocumentServiceTests.CreateDocument(),
                new TestConnectionUrl(GetDialect()),
                cancellationToken: cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    private static SchemaDeploymentService CreateService(ISchemaScriptDeploymentExecutor executor)
        => new(
            new SchemaDocumentValidator(),
            new SchemaDocumentMapper(),
            new SchemaDomainRenderer(),
            executor);

    private static IDialect GetDialect()
        => SchemaDocumentServiceTests.CreateDialects().Get("duckdb");

    private sealed class TestConnectionUrl(IDialect dialect) : ConnectionUrl("unused://")
    {
        public override IDialect Dialect => dialect;
    }

    private sealed class RecordingExecutor : ISchemaScriptDeploymentExecutor
    {
        public string? Script { get; private set; }

        public Task ExecuteAsync(
            ConnectionUrl connection,
            string script,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Script = script;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingExecutor : ISchemaScriptDeploymentExecutor
    {
        public Task ExecuteAsync(
            ConnectionUrl connection,
            string script,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("deployment failed");
    }
}
