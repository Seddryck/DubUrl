using System.Data;
using System.Data.Common;
using System.Globalization;

namespace DubUrl.Discovery;

/// <summary>Shared ADO.NET mechanics for provider-owned read-only catalog queries with normalized column aliases.</summary>
public abstract class CatalogSchemaMetadataReader : ISchemaMetadataReader
{
    public virtual void ConfigureOwnedConnection(DbConnection connection) { }
    protected abstract string TablesSql { get; }
    protected abstract string ColumnsSql { get; }
    protected abstract string KeysSql { get; }
    protected virtual string? IndexesSql => null;
    protected virtual string? ChecksSql => null;
    protected virtual string? ViewsSql => null;
    protected virtual IReadOnlySet<SchemaFeature> UnsupportedFeatures => new HashSet<SchemaFeature>();
    public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities =>
        Enum.GetValues<SchemaFeature>().ToDictionary(feature => feature,
            feature => UnsupportedFeatures.Contains(feature) ? MetadataCoverage.Unsupported : MetadataCoverage.Available);

    public async Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken = default)
    {
        var coverage = Capabilities.ToDictionary(pair => pair.Key, pair => pair.Value);
        var diagnostics = new List<DiscoveryDiagnostic>();
        async Task<List<CatalogRow>> Query(string? sql, params SchemaFeature[] features)
        {
            if (!features.Any(options.Features.Contains) && !features.Contains(SchemaFeature.Tables) && !features.Contains(SchemaFeature.Columns)) return [];
            if (sql is null) { foreach (var feature in features) coverage[feature] = MetadataCoverage.Unsupported; return []; }
            try { return await ReadCatalogRowsAsync(connection, sql, options, cancellationToken).ConfigureAwait(false); }
            catch (DbException)
            {
                foreach (var feature in features) coverage[feature] = MetadataCoverage.Inaccessible;
                // Never expose driver exception text.
                return [];
            }
        }
        var tableRows = await Query(TablesSql, SchemaFeature.Tables).ConfigureAwait(false);
        var columnRows = await Query(ColumnsSql, SchemaFeature.Columns, SchemaFeature.Defaults, SchemaFeature.Generation, SchemaFeature.Comments).ConfigureAwait(false);
        var keyRows = await Query(KeysSql, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys, SchemaFeature.UniqueConstraints).ConfigureAwait(false);
        var indexRows = await Query(IndexesSql, SchemaFeature.Indexes).ConfigureAwait(false);
        var checkRows = await Query(ChecksSql, SchemaFeature.Checks).ConfigureAwait(false);
        var viewRows = await Query(ViewsSql, SchemaFeature.Views).ConfigureAwait(false);
        var tables = new List<MetadataTable>();
        foreach (var row in tableRows.OrderBy(row => row.Text("schema_name"), StringComparer.Ordinal).ThenBy(row => row.Text("table_name"), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identity = Identity(row);
            // All filters are applied to literal catalog identities by the orchestrator. No user SQL is interpolated.
            var columns = new List<MetadataColumn>();
            foreach (var column in columnRows.Where(column => Identity(column) == identity).OrderBy(column => column.Int("ordinal")))
            {
                var mapped = MapColumn(column);
                if (!options.Features.Contains(SchemaFeature.Generation)) mapped = mapped with { Generation = null };
                if (!options.Features.Contains(SchemaFeature.Comments)) mapped = mapped with { Comment = null };
                if (column.Int("length") == -1)
                    Diagnose(SchemaFeature.Columns, identity, "An unbounded native length cannot be represented by the current column facets.");
                if (mapped.Type is null) Diagnose(SchemaFeature.Columns, identity, "A column type cannot be represented.");
                if (options.Features.Contains(SchemaFeature.Defaults) && column.Text("default_expression") is { } defaultSql && mapped.Generation is null)
                {
                    if (DiscoveryExpressions.TryLiteral(defaultSql, out var value))
                    {
                        try { mapped = mapped with { Default = ConvertDefault(value, mapped.Type) }; }
                        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
                        { Diagnose(SchemaFeature.Defaults, identity, "A default literal is incompatible with the logical column type."); }
                    }
                    else Diagnose(SchemaFeature.Defaults, identity, "A default is not a portable literal.");
                }
                if (options.Features.Contains(SchemaFeature.Generation) && column.Text("generation_expression") is { } generatedSql)
                {
                    if (DiscoveryExpressions.TryExpression(generatedSql, out var expression))
                        mapped = mapped with { Generation = new(Expression: expression, Stored: column.Bool("stored") ?? true), Default = null };
                    else Diagnose(SchemaFeature.Generation, identity, "A generated expression cannot be represented.");
                }
                columns.Add(mapped);
            }
            var keys = keyRows.Where(key => Identity(key) == identity).GroupBy(key => (Kind: key.Text("kind"), Name: key.Text("key_name"))).ToArray();
            MetadataKey Key(IEnumerable<CatalogRow> rows) => new(rows.First().Text("key_name"),
                rows.OrderBy(key => key.Int("ordinal")).Select(key => key.Text("column_name")!).ToArray());
            var indexes = new List<MetadataIndex>();
            foreach (var index in indexRows.Where(index => Identity(index) == identity).GroupBy(index => index.Text("index_name")))
            {
                if (index.Any(part => part.Bool("unsupported") == true || part.Text("column_name") is null))
                { Diagnose(SchemaFeature.Indexes, identity, "An expression, filtered, included-column, or specialized index cannot be represented."); continue; }
                indexes.Add(new(new(index.Key!, identity.Schema, identity.Database, identity.Catalog),
                    index.OrderBy(part => part.Int("ordinal")).Select(part => new MetadataIndexColumn(part.Text("column_name")!, part.Bool("descending"))).ToArray(),
                    index.First().Bool("unique_index") ?? false));
            }
            var checks = new List<MetadataCheck>();
            foreach (var check in checkRows.Where(check => Identity(check) == identity).OrderBy(check => check.Text("check_name"), StringComparer.Ordinal))
            {
                if (DiscoveryExpressions.TryCheck(check.Text("expression") ?? "", check.Text("check_name"), out var mapped)) checks.Add(mapped!);
                else Diagnose(SchemaFeature.Checks, identity, "A check expression cannot be represented.");
            }
            tables.Add(new(identity, columns)
            {
                Comment = options.Features.Contains(SchemaFeature.Comments) ? row.Text("comment") : null,
                PrimaryKey = options.Features.Contains(SchemaFeature.PrimaryKeys) ? keys.Where(key => key.Key.Kind == "PRIMARY KEY").Select(Key).SingleOrDefault() : null,
                UniqueConstraints = options.Features.Contains(SchemaFeature.UniqueConstraints) ? keys.Where(key => key.Key.Kind == "UNIQUE").OrderBy(key => key.Key.Name, StringComparer.Ordinal).Select(Key).ToArray() : [],
                ForeignKeys = options.Features.Contains(SchemaFeature.ForeignKeys) ? keys.Where(key => key.Key.Kind == "FOREIGN KEY").OrderBy(key => key.Key.Name, StringComparer.Ordinal).Select(key =>
                {
                    var ordered = key.OrderBy(part => part.Int("ordinal")).ToArray();
                    return new MetadataForeignKey(key.Key.Name!, ordered.Select(part => part.Text("column_name")!).ToArray(),
                        new(ordered[0].Text("target_table")!, ordered[0].Text("target_schema"), ordered[0].Text("target_database")),
                        ordered.Select(part => part.Text("target_column")!).ToArray());
                }).ToArray() : [],
                Indexes = indexes,
                Checks = checks
            });
        }
        var views = viewRows.GroupBy(Identity).OrderBy(group => group.Key.Schema, StringComparer.Ordinal).ThenBy(group => group.Key.Name, StringComparer.Ordinal).Select(group =>
        {
            var inventory = group.Where(row => row.Text("column_name") is not null).OrderBy(row => row.Int("ordinal")).Select(MapColumn).ToArray();
            return TryView(group.Key, inventory, group.First().Text("definition"));
        }).ToArray();
        return new(tables, coverage, diagnostics) { Views = views };

        void Diagnose(SchemaFeature feature, MetadataName identity, string message)
        {
            diagnostics.Add(new("SDI009", feature, MetadataCoverage.Unrepresentable, message, identity));
        }
    }

    private static object? ConvertDefault(object? value, DbType? type)
    {
        if (value is null) return null;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture)!;
        return type switch
        {
            DbType.Boolean when text is "0" or "1" => text == "1",
            DbType.Boolean => bool.Parse(text),
            DbType.Byte => Convert.ToByte(value, CultureInfo.InvariantCulture),
            DbType.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            DbType.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            DbType.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            DbType.Decimal or DbType.Currency => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            DbType.Single => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            DbType.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            DbType.Date or DbType.DateTime or DbType.DateTime2 => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DbType.DateTimeOffset => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DbType.Guid => Guid.Parse(text),
            DbType.Time => TimeSpan.Parse(text, CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private static MetadataView TryView(MetadataName identity, MetadataColumn[] inventory, string? sql)
    {
        if (sql is null) return new(identity, inventory);
        var text = sql.Trim().TrimEnd(';').Trim();
        // A deliberately narrow grammar: SELECT identifier [AS alias], ... FROM qualified_identifier.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^CREATE\s+(?:OR\s+REPLACE\s+)?VIEW\s+.+?\s+AS\s+", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"^SELECT\s+(.+)\s+FROM\s+(.+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        if (!match.Success) return new(identity, inventory);
        try
        {
            var source = MetadataNames.Parse(match.Groups[2].Value.Trim());
            source = source with { Schema = source.Schema ?? identity.Schema };
            var projections = new List<MetadataProjection>();
            foreach (var item in match.Groups[1].Value.Split(','))
            {
                var parts = System.Text.RegularExpressions.Regex.Split(item.Trim(), @"\s+AS\s+",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (parts.Length > 2) return new(identity, inventory);
                var column = MetadataNames.Parse(parts[0]);
                if (column.Database is not null || (column.Schema is not null && column.Schema != source.Name)) return new(identity, inventory);
                var alias = parts.Length == 2 ? MetadataNames.Parse(parts[1]) : null;
                if (alias?.Schema is not null) return new(identity, inventory);
                projections.Add(new(new("column", column.Name), alias?.Name));
            }
            return new(identity, inventory, source, projections);
        }
        catch (ArgumentException) { return new(identity, inventory); }
    }

    protected virtual MetadataColumn MapColumn(CatalogRow row)
    {
        var native = row.Text("type_name")?.ToLowerInvariant() ?? "";
        var basic = native.Split('(')[0].Trim();
        DbType? type = basic switch
        {
            "tinyint" => DbType.Byte,
            "smallint" or "int2" => DbType.Int16,
            "int" or "integer" or "int4" => DbType.Int32,
            "bigint" or "int8" => DbType.Int64,
            "boolean" or "bool" or "bit" => DbType.Boolean,
            "decimal" or "numeric" => DbType.Decimal,
            "real" or "float4" => DbType.Single,
            "float" or "double" or "double precision" or "float8" => DbType.Double,
            "char" or "character" => DbType.AnsiStringFixedLength,
            "nchar" => DbType.StringFixedLength,
            "varchar" or "character varying" or "text" => DbType.AnsiString,
            "nvarchar" or "ntext" => DbType.String,
            "binary" or "varbinary" or "bytea" or "blob" => DbType.Binary,
            "date" => DbType.Date,
            "datetime" or "timestamp" or "timestamp without time zone" => DbType.DateTime,
            "datetime2" => DbType.DateTime2,
            "timestamp with time zone" or "timestamptz" or "datetimeoffset" => DbType.DateTimeOffset,
            "time" or "time without time zone" => DbType.Time,
            "uuid" or "uniqueidentifier" => DbType.Guid,
            _ => null
        };
        var facets = System.Text.RegularExpressions.Regex.Match(native, @"^[a-z][a-z ]*\(\s*(\d+)\s*(?:,\s*(\d+)\s*)?\)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        int? declaredFirst = facets.Success && int.TryParse(facets.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var first) ? first : null;
        byte? declaredPrecision = declaredFirst is > 0 and <= 255 ? (byte)declaredFirst.Value : null;
        byte? declaredScale = facets.Groups[2].Success && byte.TryParse(facets.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var scale) ? scale : null;
        return new(row.Text("column_name")!, row.Int("ordinal") ?? 0, type, row.Bool("nullable"))
        {
            Length = type is DbType.AnsiString or DbType.String or DbType.AnsiStringFixedLength or DbType.StringFixedLength or DbType.Binary
                ? (row.Int("length") ?? declaredFirst) is > 0 and var length ? length : null : null,
            Precision = type == DbType.Decimal ? (row.Byte("precision") ?? declaredPrecision) : null,
            Scale = type == DbType.Decimal ? (row.Byte("scale") ?? declaredScale) : null,
            Comment = row.Text("comment"),
            Generation = row.Long("identity_increment") is { } increment ? new(row.Long("identity_seed") ?? 1, increment) : null
        };
    }

    /// <summary>Allows providers to normalize catalog-specific metadata before the shared mapping pipeline.</summary>
    protected virtual Task<List<CatalogRow>> ReadCatalogRowsAsync(DbConnection connection, string sql, DiscoveryOptions options, CancellationToken cancellationToken)
        => QueryAsync(connection, sql, cancellationToken);

    protected static async Task<List<CatalogRow>> QueryAsync(DbConnection connection, string sql, CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<CatalogRow>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++) values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(new(values));
        }
        return rows;
    }
    protected static MetadataName Identity(CatalogRow row) => new(row.Text("table_name")!, row.Text("schema_name"), row.Text("database_name"));
}

public sealed class CatalogRow(IReadOnlyDictionary<string, object?> values)
{
    public CatalogRow With(params (string Name, object? Value)[] replacements)
    {
        var copy = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in replacements) copy[name] = value;
        return new(copy);
    }

    public string? Text(string name) => values.GetValueOrDefault(name) is { } value ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
    public int? Int(string name) => Text(name) is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : null;
    public long? Long(string name) => Text(name) is { } value ? long.Parse(value, CultureInfo.InvariantCulture) : null;
    public byte? Byte(string name) => Int(name) is >= 0 and <= 255 and var value ? (byte)value : null;
    public bool? Bool(string name) => Text(name) is { } value ? value is "1" or "true" or "True" or "YES" : null;
}
