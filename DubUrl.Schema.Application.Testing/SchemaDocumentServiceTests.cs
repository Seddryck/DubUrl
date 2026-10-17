using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents;
using DubUrl.Schema.Documents.V1;
using NUnit.Framework;
using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Schema.Application.Testing;

public class SchemaDocumentServiceTests
{
    [Test]
    public void Validate_InvalidDocument_AccumulatesStableDiagnosticsAndPaths()
    {
        var document = new SchemaDocument
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument(),
                    Columns = [new ColumnDocument { Type = "not-a-type" }]
                }
            ]
        };

        var result = CreateService().Validate(document);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Does.Contain(SchemaDocumentDiagnosticCodes.RequiredValue));
            Assert.That(result.Diagnostics.Select(item => item.Code),
                Does.Contain(SchemaDocumentDiagnosticCodes.UnsupportedType));
            Assert.That(result.Diagnostics.Select(item => item.Path),
                Does.Contain("tables[0].identity.name"));
            Assert.That(result.Diagnostics.Select(item => item.Path),
                Does.Contain("tables[0].columns[0].type"));
        });
    }

    [Test]
    public void Render_ValidDocument_ReturnsSqlWithoutMutatingSource()
    {
        var document = CreateDocument();
        var service = CreateService();

        var first = service.Render(document, "duckdb");
        var second = service.Render(document, "duckdb");

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Sql, Does.Contain("CREATE TABLE customer"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(document.Tables[0].Identity.Name, Is.EqualTo("customer"));
            Assert.That(document.Tables[0].Columns[0].Name, Is.EqualTo("id"));
        });
    }

    [Test]
    public void Render_UnknownDialect_ReturnsStableDiagnostic()
    {
        var result = CreateService().Render(CreateDocument(), "unknown");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Diagnostics.Single().Code,
                Is.EqualTo(SchemaApplicationDiagnosticCodes.DialectNotFound));
            Assert.That(result.Diagnostics.Single().Path, Is.EqualTo("dialect"));
        });
    }

    [Test]
    public void Render_MappingFailure_ReturnsStableDiagnostic()
    {
        var service = new SchemaDocumentService(
            new SchemaDocumentValidator(),
            new FailingMapper(),
            new SchemaDialectResolver(CreateDialects()),
            new SchemaDomainRenderer());

        var result = service.Render(CreateDocument(), "duckdb");

        Assert.That(result.Diagnostics.Single().Code,
            Is.EqualTo(SchemaApplicationDiagnosticCodes.MappingFailed));
    }

    [Test]
    public void Render_RenderingFailure_ReturnsStableDiagnostic()
    {
        var service = new SchemaDocumentService(
            new SchemaDocumentValidator(),
            new SchemaDocumentMapper(),
            new SchemaDialectResolver(CreateDialects()),
            new FailingRenderer());

        var result = service.Render(CreateDocument(), "duckdb");

        Assert.That(result.Diagnostics.Single().Code,
            Is.EqualTo(SchemaApplicationDiagnosticCodes.RenderingFailed));
    }

    [Test]
    public void Render_TwoHostAdapters_ReceiveEquivalentSemanticResults()
    {
        ISchemaDocumentService service = CreateService();
        var document = CreateDocument();

        var commandLineHost = InvokeFromCommandLineHost(service, document);
        var workerHost = InvokeFromWorkerHost(service, document);

        Assert.That(workerHost, Is.EqualTo(commandLineHost));
    }

    private static (bool Success, string? Sql, string[] Codes) InvokeFromCommandLineHost(
        ISchemaDocumentService service,
        ISchemaDocument document)
    {
        var result = service.Render(document, "duckdb");
        return (result.IsSuccess, result.Sql, result.Diagnostics.Select(item => item.Code).ToArray());
    }

    private static (bool Success, string? Sql, string[] Codes) InvokeFromWorkerHost(
        ISchemaDocumentService service,
        ISchemaDocument document)
    {
        var result = service.Render(document, "duckdb");
        return (result.IsSuccess, result.Sql, result.Diagnostics.Select(item => item.Code).ToArray());
    }

    private static SchemaDocumentService CreateService()
        => new(
            new SchemaDocumentValidator(),
            new SchemaDocumentMapper(),
            new SchemaDialectResolver(CreateDialects()),
            new SchemaDomainRenderer());

    internal static IDialectRegistry CreateDialects()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<DuckDbDialect>(["duckdb"]);
        return builder.Build();
    }

    internal static SchemaDocument CreateDocument()
        => new()
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "customer" },
                    Columns = [new ColumnDocument { Name = "id", Type = "int32", Nullable = false }]
                }
            ]
        };

    private sealed class FailingMapper : ISchemaDocumentMapper
    {
        public RuntimeSchema Map(ISchemaDocument document)
            => throw new SchemaDocumentMappingException("mapping failed", new InvalidOperationException());
    }

    private sealed class FailingRenderer : ISchemaDomainRenderer
    {
        public string Render(RuntimeSchema schema, IDialect dialect, SchemaCreationOptions options)
            => throw new NotSupportedException("rendering failed");
    }
}
