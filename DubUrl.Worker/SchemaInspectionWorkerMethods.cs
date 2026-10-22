using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Schema;
using DubUrl.Schema.Documents.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Worker;

public sealed record SchemaInspectParameters(string? Connection, IReadOnlyList<MetadataName>? Tables = null,
    IReadOnlyList<string>? Schemas = null, bool AllowPartial = false, bool IncludeReferences = false, bool Basic = false);
public sealed record SchemaInspectionResponse(bool Success, SchemaDocument? Document, bool IsComplete,
    IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Coverage, IReadOnlyList<DiscoveryDiagnostic> Diagnostics);

internal static partial class SchemaWorkerMethods
{
    private static void RegisterInspection(WorkerMethodRegistry registry, IServiceProvider services)
    {
        registry.Register<SchemaInspectParameters, SchemaInspectionResponse>("schema.inspect",
            "Inspect a configured connection and return a v1 document with independent discovery coverage and completeness.",
            (parameters, cancellationToken) => InspectAsync(services, parameters, cancellationToken));
        var schemes = DefaultRegistry.GetDialects().GetAll().SelectMany(dialect => dialect.Aliases)
            .Where(alias => DefaultRegistry.Get().TryGetCapability<SchemaDiscoveryCapability>(DubUrl.Parsing.SchemeKey.Parse(alias), out _))
            .Order(StringComparer.Ordinal).ToArray();
        registry.SetFeature("schemaDiscovery", new { schemes, strictByDefault = true, readOnly = true });
    }
    private static async ValueTask<SchemaInspectionResponse> InspectAsync(IServiceProvider services,
        SchemaInspectParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(parameters.Connection))
            throw new WorkerInvalidParametersException("A configured connection name is required.");
        if (parameters.Tables?.Any(table => table is null || string.IsNullOrWhiteSpace(table.Name)) == true
            || parameters.Schemas?.Any(string.IsNullOrWhiteSpace) == true)
            throw new WorkerInvalidParametersException("Filters must contain non-empty identifiers.");
        var value = services.GetRequiredService<IConfiguration>()["ConnectionStrings:" + parameters.Connection];
        if (string.IsNullOrWhiteSpace(value))
            return new(false, null, false, new Dictionary<SchemaFeature, MetadataCoverage>(),
                [new("SDW002", SchemaFeature.Tables, MetadataCoverage.Inaccessible, "The named connection was not found in worker configuration.")]);
        var options = new DiscoveryOptions
        {
            Tables = parameters.Tables ?? [],
            Schemas = parameters.Schemas ?? [],
            AllowPartial = parameters.AllowPartial,
            IncludeReferencedTables = parameters.IncludeReferences,
            Features = parameters.Basic ? [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys] : Enum.GetValues<SchemaFeature>()
        };
        var operation = await services.GetRequiredService<ISchemaInspectionService>().InspectAsync(
            new ConnectionUrl(value), options, cancellationToken).ConfigureAwait(false);
        return new(operation.IsSuccess, operation.Document, operation.Inspection.IsComplete, operation.Inspection.Coverage, operation.Diagnostics);
    }
}
