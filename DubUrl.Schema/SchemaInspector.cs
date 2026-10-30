using DubUrl.Discovery;
using DubUrl.Schema.Constraints;
using System.Data.Common;

namespace DubUrl.Schema;

public sealed record SchemaInspectionResult(Schema? Schema, bool IsComplete, bool IsSuccess,
    IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Coverage, IReadOnlyList<DiscoveryDiagnostic> Diagnostics)
{
    public IReadOnlyList<MetadataView> ViewInventory { get; init; } = [];
}

/// <summary>Maps provider-neutral metadata. No SQL is executed by this class.</summary>
public sealed class SchemaInspector
{
    public async Task<SchemaInspectionResult> InspectAsync(ConnectionUrl connection, DiscoveryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        options ??= new();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!connection.TryGetCapability<SchemaDiscoveryCapability>(out var capability))
                return Failure("SDI001", MetadataCoverage.Unsupported, "The provider does not support schema discovery.");
            using var owned = await connection.OpenAsync(capability!.Reader.ConfigureOwnedConnection, cancellationToken).ConfigureAwait(false);
            return await InspectAsync(owned, capability!.Reader, options, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException or DubUrlException or NotSupportedException or ArgumentException or FormatException)
        {
            // Driver exception messages can contain credentials and connection strings.
            return Failure("SDI002", MetadataCoverage.Inaccessible, "Database metadata could not be accessed.");
        }
    }

    public async Task<SchemaInspectionResult> InspectAsync(DbConnection borrowed, ISchemaMetadataReader reader,
        DiscoveryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(borrowed);
        ArgumentNullException.ThrowIfNull(reader);
        options ??= new();
        cancellationToken.ThrowIfCancellationRequested();
        if (borrowed.State != System.Data.ConnectionState.Open)
            throw new ArgumentException("A borrowed connection must already be open.", nameof(borrowed));
        SchemaMetadata metadata;
        try { metadata = await reader.ReadAsync(borrowed, options, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is DbException or InvalidOperationException or TimeoutException or NotSupportedException)
        { return Failure("SDI002", MetadataCoverage.Inaccessible, "Database metadata could not be accessed."); }
        cancellationToken.ThrowIfCancellationRequested();
        return Map(metadata, options, cancellationToken);
    }

    public SchemaInspectionResult Map(SchemaMetadata metadata, DiscoveryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        cancellationToken.ThrowIfCancellationRequested();
        try { return MapCore(metadata, options ?? new(), cancellationToken); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
        { return Failure("SDI007", MetadataCoverage.Unrepresentable, "Metadata could not be mapped into a valid schema."); }
    }

    private static SchemaInspectionResult MapCore(SchemaMetadata metadata, DiscoveryOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        options ??= new();
        var diagnostics = new List<DiscoveryDiagnostic>();
        var coverage = options.Features.Concat(new[] { SchemaFeature.Tables, SchemaFeature.Columns }).Distinct().ToDictionary(feature => feature,
            feature => metadata.Coverage.GetValueOrDefault(feature, MetadataCoverage.Unsupported));
        foreach (var item in coverage.Where(item => item.Value is not (MetadataCoverage.Available or MetadataCoverage.Absent)))
            diagnostics.Add(new("SDI003", item.Key, item.Value, "Requested metadata is not completely available."));
        var selected = SelectTables(metadata, options, diagnostics);
        diagnostics.AddRange(metadata.Diagnostics.Where(item => options.Features.Contains(item.Feature) && (item.Object is null || selected.ContainsKey(item.Object))));
        var tables = new List<Table>();
        var indexes = new List<Index>();
        foreach (var table in selected.Values.OrderBy(table => Name(table.Identity).Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                tables.Add(MapTable(table, selected, diagnostics));
                indexes.AddRange(MapIndexes(table));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException)
            {
                return new(null, false, false, coverage, diagnostics.Append(new DiscoveryDiagnostic("SDI007",
                    SchemaFeature.Tables, MetadataCoverage.Unrepresentable, "Metadata could not be mapped without losing semantics.", table.Identity)).ToArray());
            }
        }
        var views = MapViews(metadata, options, selected, diagnostics);
        return CompleteResult(metadata, options, diagnostics, coverage, tables, indexes, views);
    }

    private static SchemaInspectionResult CompleteResult(SchemaMetadata metadata, DiscoveryOptions options,
        List<DiscoveryDiagnostic> diagnostics, Dictionary<SchemaFeature, MetadataCoverage> coverage,
        List<Table> tables, List<Index> indexes, List<View> views)
    {
        try
        {
            var schema = new Schema(tables.ToArray(), indexes.ToArray(), views.ToArray());
            ApplyDiagnosticCoverage(coverage, diagnostics);
            var complete = diagnostics.Count == 0;
            return new(schema, complete, complete || options.AllowPartial, coverage, diagnostics) { ViewInventory = metadata.Views.Where(view => options.Tables.Count == 0 && (options.Schemas.Count == 0 || options.Schemas.Contains(view.Identity.Schema, StringComparer.Ordinal))).ToArray() };
        }
        catch (ArgumentException)
        {
            return new(null, false, false, coverage, diagnostics.Append(new DiscoveryDiagnostic("SDI007", SchemaFeature.Tables,
            MetadataCoverage.Unrepresentable, "The discovered relationships do not form a valid schema.")).ToArray());
        }
    }

    private static Dictionary<MetadataName, MetadataTable> SelectTables(SchemaMetadata metadata, DiscoveryOptions options, List<DiscoveryDiagnostic> diagnostics)
    {
        var all = metadata.Tables.ToDictionary(table => table.Identity);
        var selected = metadata.Tables.Where(table =>
            (options.Schemas.Count == 0 || options.Schemas.Contains(table.Identity.Schema, StringComparer.Ordinal))
            && (options.Tables.Count == 0 || options.Tables.Any(filter => Matches(filter, table.Identity)))).ToDictionary(table => table.Identity);
        foreach (var filter in options.Tables.Where(filter => !selected.Keys.Any(name => Matches(filter, name))))
            diagnostics.Add(new("SDI004", SchemaFeature.Tables, MetadataCoverage.Absent, "A selected table was not found or is not visible.", filter));
        if (options.IncludeReferencedTables) IncludeReferences(selected, all);
        return selected;
    }

    private static void IncludeReferences(Dictionary<MetadataName, MetadataTable> selected, Dictionary<MetadataName, MetadataTable> all)
    {
        var queue = new Queue<MetadataTable>(selected.Values);
        while (queue.TryDequeue(out var table))
            foreach (var key in table.ForeignKeys)
                if (!selected.ContainsKey(key.Target) && all.TryGetValue(key.Target, out var target))
                { selected.Add(target.Identity, target); queue.Enqueue(target); }
    }

    private static Table MapTable(MetadataTable table, Dictionary<MetadataName, MetadataTable> selected, List<DiscoveryDiagnostic> diagnostics)
    {
        if (table.Columns.Count == 0) throw new NotSupportedException();
        var columns = table.Columns.OrderBy(column => column.Ordinal).Select(column => MapColumn(column)).ToArray();
        var byName = columns.ToDictionary(column => column.Name, StringComparer.Ordinal);
        var constraints = new List<Constraint>();
        if (table.PrimaryKey is { } pk)
            constraints.Add(new PrimaryKeyConstraint(pk.Columns.Select(name => byName[name]).ToArray(), pk.Name));
        constraints.AddRange(MapForeignKeys(table, selected, diagnostics));
        constraints.AddRange(table.Checks.OrderBy(check => check.Name, StringComparer.Ordinal).Select(check => new CheckConstraint(
            Expression(check.Left), check.Operator, Expression(check.Right), check.Name)));
        // v1 has no named table unique-constraint construct; never turn a constraint into an index.
        foreach (var unique in table.UniqueConstraints)
            diagnostics.Add(new("SDI006", SchemaFeature.UniqueConstraints, MetadataCoverage.Unrepresentable,
                "A named table unique constraint is not representable in the schema model.", table.Identity));
        return new Table(Name(table.Identity), columns, constraints.ToArray(), table.Comment);
    }

    private static IEnumerable<Index> MapIndexes(MetadataTable table)
    {
        return table.Indexes.OrderBy(index => Name(index.Identity).Key, StringComparer.Ordinal).Select(index =>
            new Index(Name(index.Identity), Name(table.Identity), index.Columns.Select(column =>
                new IndexColumn(column.Name, column.Descending is null ? null :
                    column.Descending.Value ? IndexSortDirection.Descending : IndexSortDirection.Ascending)).ToArray(), index.Unique));
    }

    private static List<Constraint> MapForeignKeys(MetadataTable table, Dictionary<MetadataName, MetadataTable> selected, List<DiscoveryDiagnostic> diagnostics)
    {
        var constraints = new List<Constraint>();
        foreach (var fk in table.ForeignKeys.OrderBy(key => key.Name, StringComparer.Ordinal))
        {
            if (!selected.ContainsKey(fk.Target))
            {
                diagnostics.Add(new("SDI005", SchemaFeature.ForeignKeys, MetadataCoverage.Unrepresentable,
                    "A foreign-key target is outside the selected or visible tables; the key was omitted.", table.Identity));
                continue;
            }
            constraints.Add(new ForeignKeyConstraint(fk.Name, fk.Columns, Name(fk.Target), fk.TargetColumns));
        }
        return constraints;
    }

    private static List<View> MapViews(SchemaMetadata metadata, DiscoveryOptions options, Dictionary<MetadataName, MetadataTable> selected, List<DiscoveryDiagnostic> diagnostics)
    {
        var views = new List<View>();
        foreach (var view in metadata.Views.Where(_ => options.Features.Contains(SchemaFeature.Views)).OrderBy(view => Name(view.Identity).Key, StringComparer.Ordinal))
        {
            if (options.Schemas.Count > 0 && !options.Schemas.Contains(view.Identity.Schema, StringComparer.Ordinal)) continue;
            if (options.Tables.Count > 0) continue; // Table filters select tables, not views.
            if (view.Source is not null && (selected.ContainsKey(view.Source) || metadata.Views.Any(candidate => candidate.Identity == view.Source)) && view.Projections is not null)
                views.Add(new View(Name(view.Identity), new ViewQuery(Name(view.Source),
                    view.Projections.Select(expression => new ViewProjection(Expression(expression.Expression), expression.Alias)).ToArray())));
            else diagnostics.Add(new("SDI008", SchemaFeature.Views, MetadataCoverage.Unrepresentable,
                "The view definition cannot be represented by a portable projection query.", view.Identity));
        }
        return views;
    }

    private static void ApplyDiagnosticCoverage(Dictionary<SchemaFeature, MetadataCoverage> coverage, List<DiscoveryDiagnostic> diagnostics)
    {
        foreach (var group in diagnostics.GroupBy(item => item.Feature))
            if (coverage.ContainsKey(group.Key))
                coverage[group.Key] = group.Any(item => item.Coverage == MetadataCoverage.Inaccessible)
                    ? MetadataCoverage.Inaccessible : group.Any(item => item.Coverage == MetadataCoverage.Unrepresentable)
                        ? MetadataCoverage.Unrepresentable : group.First().Coverage;
    }

    private static NativeDatabaseType? MapNativeType(MetadataColumn column)
    {
        NativeDatabaseType? native = null;
        if (column.NativeType is not null)
        {
            if (column.NativeDialect is null) throw new NotSupportedException();
            native = new NativeDatabaseType(column.NativeType, column.NativeDialect);
        }
        return native;
    }

    private static ColumnGeneration? MapGeneration(MetadataColumn column)
    {
        return column.Generation is not { } g ? null : g.Expression is not null
            ? new GeneratedColumnGeneration(Expression(g.Expression), g.Stored ? GeneratedColumnStorage.Stored : GeneratedColumnStorage.Virtual)
            : new IdentityColumnGeneration(g.Seed ?? 1, g.Increment ?? 1);
    }

    private static Column MapColumn(MetadataColumn column)
    {
        if (column.Type is null || column.Nullable is null) throw new NotSupportedException();
        var constraints = new IConstraint[] { column.Nullable.Value ? new NullableConstraint() : new NotNullableConstraint() };
        var native = MapNativeType(column);
        var generation = MapGeneration(column);
        var type = column.Type.Value;
        return column.Length is { } length
            ? new VarLengthColumn(column.Name, type, length, column.Default, constraints, column.Comment, native, generation: generation)
            : column.Precision is { } precision
                ? new NumericColumn(column.Name, type, precision, column.Scale ?? 0, column.Default, constraints, column.Comment, native, generation: generation)
                : new Column(column.Name, type, column.Default, constraints, column.Comment, native, generation: generation);
    }

    private static Expression Expression(MetadataExpression expression) => expression.Kind switch
    {
        "column" => new ColumnIdentityExpression(expression.Name!),
        "value" => new ValueExpression(expression.Value!),
        "function" => new FunctionExpression(expression.Name!, (expression.Arguments ?? []).Select(Expression).ToArray()),
        _ => throw new NotSupportedException("Unsupported metadata expression.")
    };
    private static DatabaseObjectName Name(MetadataName name) => new(name.Name, name.Schema, name.Database, name.Catalog);
    private static bool Matches(MetadataName filter, MetadataName name) =>
        filter.Name == name.Name && (filter.Schema is null || filter.Schema == name.Schema)
        && (filter.Database is null || filter.Database == name.Database) && (filter.Catalog is null || filter.Catalog == name.Catalog);
    private static SchemaInspectionResult Failure(string code, MetadataCoverage status, string message) =>
        new(null, false, false, new Dictionary<SchemaFeature, MetadataCoverage>(),
            [new(code, SchemaFeature.Tables, status, message)]);
}
