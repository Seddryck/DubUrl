using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using DubUrl.Discovery;
using DubUrl.Provider.MySql.Sql;

namespace DubUrl.Provider.MySql.Discovery;

/// <summary>Read-only MySQL 8 catalog discovery. Compatible protocols are deliberately not opted in.</summary>
public sealed class MySqlMetadataReader : CatalogSchemaMetadataReader
{
    protected override string TablesSql => """
        SELECT TABLE_SCHEMA AS database_name,TABLE_NAME AS table_name,NULLIF(TABLE_COMMENT,'') AS comment
        FROM information_schema.tables
        WHERE TABLE_TYPE='BASE TABLE' AND TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
        """;
    protected override string ColumnsSql => """
        SELECT TABLE_SCHEMA AS database_name,TABLE_NAME AS table_name,COLUMN_NAME AS column_name,
          ORDINAL_POSITION AS ordinal,DATA_TYPE AS type_name,COLUMN_TYPE AS native_type_name,
          IS_NULLABLE AS nullable,CASE WHEN DATA_TYPE IN ('char','varchar','binary','varbinary') THEN CHARACTER_MAXIMUM_LENGTH END AS length,
          NUMERIC_PRECISION AS `precision`,NUMERIC_SCALE AS scale,DATETIME_PRECISION AS temporal_precision,COLUMN_DEFAULT AS default_expression,
          EXTRA AS extra,NULLIF(GENERATION_EXPRESSION,'') AS generation_expression,
          EXTRA LIKE '%STORED GENERATED%' AS `stored`,NULLIF(COLUMN_COMMENT,'') AS comment
        FROM information_schema.columns
        WHERE TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
        """;
    protected override string KeysSql => """
        SELECT t.TABLE_SCHEMA AS database_name,t.TABLE_NAME AS table_name,t.CONSTRAINT_NAME AS key_name,
          t.CONSTRAINT_TYPE AS kind,k.ORDINAL_POSITION AS ordinal,k.COLUMN_NAME AS column_name,
          k.REFERENCED_TABLE_SCHEMA AS target_database,k.REFERENCED_TABLE_NAME AS target_table,
          k.REFERENCED_COLUMN_NAME AS target_column,
          (k.COLUMN_NAME IS NULL OR EXISTS (SELECT 1 FROM information_schema.statistics s WHERE s.TABLE_SCHEMA=t.TABLE_SCHEMA
            AND s.TABLE_NAME=t.TABLE_NAME AND s.INDEX_NAME=t.CONSTRAINT_NAME
            AND (s.SUB_PART IS NOT NULL OR s.COLUMN_NAME IS NULL))) AS unsupported
        FROM information_schema.table_constraints t LEFT JOIN information_schema.key_column_usage k
          ON t.CONSTRAINT_SCHEMA=k.CONSTRAINT_SCHEMA AND t.TABLE_NAME=k.TABLE_NAME AND t.CONSTRAINT_NAME=k.CONSTRAINT_NAME
        WHERE t.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE','FOREIGN KEY')
          AND t.TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
        """;
    protected override string IndexesSql => """
        SELECT s.TABLE_SCHEMA AS database_name,s.TABLE_NAME AS table_name,s.INDEX_NAME AS index_name,
          s.NON_UNIQUE=0 AS unique_index,s.SEQ_IN_INDEX AS ordinal,s.COLUMN_NAME AS column_name,
          CASE s.COLLATION WHEN 'D' THEN 1 WHEN 'A' THEN 0 END AS descending,
          (s.COLUMN_NAME IS NULL OR s.SUB_PART IS NOT NULL OR s.INDEX_TYPE<>'BTREE' OR s.IS_VISIBLE='NO') AS unsupported
        FROM information_schema.statistics s
        WHERE s.TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
          AND NOT EXISTS (SELECT 1 FROM information_schema.table_constraints t
            WHERE t.TABLE_SCHEMA=s.TABLE_SCHEMA AND t.TABLE_NAME=s.TABLE_NAME
              AND t.CONSTRAINT_NAME=s.INDEX_NAME AND t.CONSTRAINT_TYPE IN ('PRIMARY KEY','UNIQUE'))
        """;
    protected override string ChecksSql => """
        SELECT t.TABLE_SCHEMA AS database_name,t.TABLE_NAME AS table_name,t.CONSTRAINT_NAME AS check_name,
          CASE WHEN t.ENFORCED='YES' THEN c.CHECK_CLAUSE ELSE '<unenforced check>' END AS expression
        FROM information_schema.table_constraints t JOIN information_schema.check_constraints c
          ON c.CONSTRAINT_SCHEMA=t.CONSTRAINT_SCHEMA AND c.CONSTRAINT_NAME=t.CONSTRAINT_NAME
        WHERE t.CONSTRAINT_TYPE='CHECK' AND t.TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
        """;
    protected override string ViewsSql => """
        SELECT v.TABLE_SCHEMA AS database_name,v.TABLE_NAME AS table_name,v.VIEW_DEFINITION AS definition,
          c.COLUMN_NAME AS column_name,c.ORDINAL_POSITION AS ordinal,c.DATA_TYPE AS type_name,
          c.COLUMN_TYPE AS native_type_name,c.IS_NULLABLE AS nullable,
          CASE WHEN c.DATA_TYPE IN ('char','varchar','binary','varbinary') THEN c.CHARACTER_MAXIMUM_LENGTH END AS length,
          c.NUMERIC_PRECISION AS `precision`,c.NUMERIC_SCALE AS scale,c.DATETIME_PRECISION AS temporal_precision
        FROM information_schema.views v LEFT JOIN information_schema.columns c
          ON c.TABLE_SCHEMA=v.TABLE_SCHEMA AND c.TABLE_NAME=v.TABLE_NAME
        WHERE v.TABLE_SCHEMA NOT IN ('mysql','sys','performance_schema','information_schema')
        """;

    protected override async Task<List<CatalogRow>> ReadCatalogRowsAsync(DbConnection connection, string sql, DiscoveryOptions options, CancellationToken cancellationToken)
    {
        var rows = await base.ReadCatalogRowsAsync(connection, sql, options, cancellationToken).ConfigureAwait(false);
        if (sql != ColumnsSql) return rows;
        // COLUMN_DEFAULT contains unquoted literal text; DEFAULT_GENERATED distinguishes SQL expressions.
        return rows.Select(row => row.Text("default_expression") is { } value
            && !(row.Text("extra") ?? "").Contains("DEFAULT_GENERATED", StringComparison.Ordinal)
            ? row.With(("default_expression", "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'")) : row).ToList();
    }

    protected override MetadataColumn MapColumn(CatalogRow row)
    {
        var column = base.MapColumn(row);
        var native = row.Text("native_type_name") ?? row.Text("type_name") ?? "";
        // Do not silently turn unsigned integers or display modifiers into ordinary signed types.
        if (native.Contains("unsigned", StringComparison.OrdinalIgnoreCase) || native.Contains("zerofill", StringComparison.OrdinalIgnoreCase))
            return column with { Type = null };
        return row.Text("type_name")?.ToLowerInvariant() switch
        {
            "tinyint" => column with { Type = DbType.SByte },
            "float" => column with { Type = DbType.Single },
            "char" => column with { Type = DbType.StringFixedLength },
            "binary" => column with { NativeType = "BINARY", NativeDialect = typeof(MySqlDialect) },
            "datetime" => column with { NativeType = "DATETIME", NativeDialect = typeof(MySqlDialect) },
            "bit" when native != "bit(1)" => column with { Type = null },
            "bit" => column with { NativeType = "BIT", NativeDialect = typeof(MySqlDialect) },
            "text" => column with { Type = DbType.String, Length = null },
            "blob" => column with { Length = null, NativeType = "BLOB", NativeDialect = typeof(MySqlDialect) },
            "json" or "geometry" => column with { Type = DbType.String, Length = null, NativeType = native.ToUpperInvariant(), NativeDialect = typeof(MySqlDialect) },
            _ => column
        };
    }

    protected override MetadataColumn MapCatalogColumn(CatalogRow row, DiscoveryOptions options, Action<SchemaFeature, string> diagnose)
    {
        var column = base.MapCatalogColumn(row, options, diagnose);
        if (row.Int("temporal_precision") > 0)
            diagnose(SchemaFeature.Columns, "Fractional temporal precision cannot be represented by the current column facets.");
        if (options.Features.Contains(SchemaFeature.Generation) && (row.Text("extra") ?? "").Contains("on update", StringComparison.OrdinalIgnoreCase))
            diagnose(SchemaFeature.Generation, "An ON UPDATE expression cannot be represented.");
        if (options.Features.Contains(SchemaFeature.Generation) && (row.Text("extra") ?? "").Contains("auto_increment", StringComparison.Ordinal))
        {
            // The catalog exposes the next table value, not the original seed or session increment.
            diagnose(SchemaFeature.Generation, "AUTO_INCREMENT is present, but its original seed and increment cannot be recovered from the catalog.");
            column = column with { Generation = null, Default = null };
        }
        return column;
    }

    protected override MetadataTable MapTable(CatalogRow row, List<CatalogRow> columns, List<CatalogRow> keys, List<CatalogRow> indexes, List<CatalogRow> checks, DiscoveryOptions options, Action<SchemaFeature, string> diagnose)
    {
        var invalid = keys.Where(key => Identity(key) == Identity(row) && key.Bool("unsupported") == true)
            .Select(key => (Kind: key.Text("kind"), Name: key.Text("key_name"))).Distinct().ToArray();
        foreach (var key in invalid)
        {
            var feature = key.Kind == "PRIMARY KEY" ? SchemaFeature.PrimaryKeys : SchemaFeature.UniqueConstraints;
            if (options.Features.Contains(feature)) diagnose(feature, "A prefix or expression key cannot be represented.");
        }
        return base.MapTable(row, columns, keys.Where(key => Identity(key) != Identity(row)
            || !invalid.Contains((key.Text("kind"), key.Text("key_name")))).ToList(), indexes, checks, options, diagnose);
    }

    protected override MetadataView MapView(MetadataName identity, MetadataColumn[] inventory, string? sql)
    {
        var unknown = new MetadataView(identity, inventory);
        if (sql is null) return unknown;
        var match = Regex.Match(sql.Trim().TrimEnd(';'), @"^SELECT\s+(.+)\s+FROM\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success) return unknown;
        try
        {
            var parsed = MetadataNames.Parse(match.Groups[2].Value.Trim());
            if (parsed.Database is not null || parsed.Catalog is not null) return unknown;
            var source = new MetadataName(parsed.Name, Database: parsed.Schema ?? identity.Database);
            var projections = ParseProjections(match.Groups[1].Value, source);
            return projections is null ? unknown : new(identity, inventory, source, projections);
        }
        catch (ArgumentException) { return unknown; }
    }

    private static List<MetadataProjection>? ParseProjections(string sql, MetadataName source)
    {
        var projections = new List<MetadataProjection>();
        foreach (var item in sql.Split(','))
        {
            var projection = ParseProjection(item, source);
            if (projection is null) return null;
            projections.Add(projection);
        }
        return projections;
    }

    private static MetadataProjection? ParseProjection(string sql, MetadataName source)
    {
        var parts = Regex.Split(sql.Trim(), @"\s+AS\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (parts.Length > 2) return null;
        var column = MetadataNames.Parse(parts[0]);
        if (column.Catalog is not null || (column.Database is not null && column.Database != source.Database)
            || (column.Schema is not null && column.Schema != source.Name)) return null;
        var alias = parts.Length == 2 ? MetadataNames.Parse(parts[1]) : null;
        return alias?.Schema is not null ? null : new(new("column", column.Name), alias?.Name);
    }
}
