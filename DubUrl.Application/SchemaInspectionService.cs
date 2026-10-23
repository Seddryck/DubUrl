using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Application;

/// <summary>Inspection and conversion results retain completeness independently of serialization.</summary>
public sealed record SchemaExportResult(bool IsSuccess, SchemaInspectionResult Inspection, SchemaDocument? Document,
    IReadOnlyList<DiscoveryDiagnostic> Diagnostics);

public interface ISchemaInspectionService
{
    Task<SchemaExportResult> InspectAsync(ConnectionUrl connection, DiscoveryOptions? options = null, CancellationToken cancellationToken = default);
    Task<SchemaExportResult> InspectAsync(DbConnection borrowed, ISchemaMetadataReader reader,
        DiscoveryOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>Transport-neutral inspection/export orchestration; hosts own output streams and serialization.</summary>
public sealed class SchemaInspectionService : ISchemaInspectionService
{
    private readonly SchemaInspector inspector;
    private readonly SchemaDocumentExporter exporter;
    public SchemaInspectionService() : this(new(), new()) { }
    public SchemaInspectionService(SchemaInspector inspector, SchemaDocumentExporter exporter)
        => (this.inspector, this.exporter) = (inspector, exporter);

    public async Task<SchemaExportResult> InspectAsync(ConnectionUrl connection, DiscoveryOptions? options = null, CancellationToken cancellationToken = default)
        => Export(await inspector.InspectAsync(connection, options, cancellationToken).ConfigureAwait(false));

    public async Task<SchemaExportResult> InspectAsync(DbConnection borrowed, ISchemaMetadataReader reader,
        DiscoveryOptions? options = null, CancellationToken cancellationToken = default)
        => Export(await inspector.InspectAsync(borrowed, reader, options, cancellationToken).ConfigureAwait(false));

    private SchemaExportResult Export(SchemaInspectionResult inspection)
    {
        if (!inspection.IsSuccess || inspection.Schema is null) return new(false, inspection, null, inspection.Diagnostics);
        try { return new(true, inspection, exporter.Map(inspection.Schema), inspection.Diagnostics); }
        catch (SchemaDocumentMappingException)
        {
            return new(false, inspection, null, inspection.Diagnostics.Append(new DiscoveryDiagnostic("SDI010",
                SchemaFeature.Tables, MetadataCoverage.Unrepresentable, "The inspected schema cannot be exported to document version 1 without loss.")).ToArray());
        }
    }
}
