using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using DubUrl.Discovery;
using DubUrl.Provider.MySql.Sql;

namespace DubUrl.Provider.MySql.Discovery;

/// <summary>Read-only SingleStore metadata. Foreign keys and checks are not enforced by this engine.</summary>
public sealed class SingleStoreMetadataReader : ISchemaMetadataReader
{
    private readonly CatalogReader reader = new();
    public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities => reader.Capabilities;

    public async Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken = default)
    {
        var metadata = await reader.ReadAsync(connection, options, cancellationToken).ConfigureAwait(false);
        // MySQL-family two-part identities denote database.object rather than schema.object.
        return metadata with { Views = metadata.Views.Select(view => view with
        {
            Source = view.Source is { } source
                ? source with { Database = source.Schema ?? view.Identity.Database, Schema = null } : null
        }).ToArray() };
    }

    private const string IdentifierPattern = @"(?:`(?:[^`]|``)+`|[A-Za-z_][A-Za-z0-9_$]*)";
    private const RegexOptions ViewRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline;

    // Normalize the narrow native SELECT form, including expanded database.table.column
    // projections and the connector's persisted result-count hint. Other SQL stays unsupported.
    internal static string? NormalizeViewDefinition(string? definition)
    {
        if (definition is null) return null;
        definition = Regex.Replace(definition, @"\s*/\*![0-9]+\s+OPTION\(CLIENT_FOUND_ROWS=[01]\)\*/\s*$", "", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var match = Regex.Match(definition.Trim().TrimEnd(';'),
            @"^SELECT\s+(?<columns>.+)\s+FROM\s+(?<source>" + IdentifierPattern + @"(?:\s*\.\s*" + IdentifierPattern + @")?)(?:\s+(?:AS\s+)?(?<alias>" + IdentifierPattern + @"))?\s*$", ViewRegexOptions, TimeSpan.FromSeconds(1));
        if (!match.Success) return definition;
        try
        {
            var source = MetadataNames.Parse(match.Groups["source"].Value);
            var alias = match.Groups["alias"].Success ? MetadataNames.Parse(match.Groups["alias"].Value).Name : source.Name;
            var projections = match.Groups["columns"].Value.Split(',').Select(item => NormalizeProjection(item, source, alias)).ToArray();
            return projections.Any(projection => projection is null) ? definition
                : "SELECT " + string.Join(", ", projections) + " FROM " + match.Groups["source"].Value;
        }
        catch (ArgumentException) { return definition; }
    }

    private static string? NormalizeProjection(string item, MetadataName source, string alias)
    {
        var projection = Regex.Match(item.Trim(), @"^(?<column>" + IdentifierPattern + @"(?:\s*\.\s*" + IdentifierPattern + @"){0,2})(?:\s+AS\s+(?<alias>" + IdentifierPattern + @"))?$", ViewRegexOptions, TimeSpan.FromSeconds(1));
        if (!projection.Success) return null;
        var column = MetadataNames.Parse(projection.Groups["column"].Value);
        if ((column.Database is not null && column.Database != source.Schema) ||
            (column.Schema is not null && column.Schema != source.Name && column.Schema != alias)) return null;
        var columnSql = "`" + column.Name.Replace("`", "``") + "`";
        if (projection.Groups["alias"].Success) columnSql += " AS " + projection.Groups["alias"].Value;
        return columnSql;
    }
    private sealed class CatalogReader : CatalogSchemaMetadataReader
    {
        protected override IReadOnlySet<SchemaFeature> UnsupportedFeatures { get; } = new HashSet<SchemaFeature>
            { SchemaFeature.ForeignKeys, SchemaFeature.Checks };
        protected override string TablesSql => """
            SELECT table_schema AS database_name, table_name, NULLIF(table_comment,'') AS comment
            FROM information_schema.tables
            WHERE table_type='BASE TABLE' AND table_schema NOT IN ('information_schema','memsql','cluster','mysql','performance_schema','sys')
            """;
        protected override string ColumnsSql => """
            SELECT table_schema AS database_name, table_name, column_name, ordinal_position AS ordinal,
              data_type AS type_name, column_type, is_nullable AS nullable, CASE WHEN character_maximum_length<=2147483647 THEN character_maximum_length END AS length,
              numeric_precision AS `precision`, numeric_scale AS `scale`, column_default AS default_expression,
              CASE WHEN extra LIKE '%auto_increment%' THEN 1 END AS identity_seed,
              CASE WHEN extra LIKE '%auto_increment%' THEN 1 END AS identity_increment,
              NULLIF(generation_expression,'') AS generation_expression, TRUE AS stored,
              NULLIF(column_comment,'') AS comment
            FROM information_schema.columns
            WHERE table_schema NOT IN ('information_schema','memsql','cluster','mysql','performance_schema','sys')
            """;
        protected override string KeysSql => """
            SELECT table_schema AS database_name, table_name, index_name AS key_name,
              CASE WHEN index_name='PRIMARY' THEN 'PRIMARY KEY' ELSE 'UNIQUE' END AS kind,
              seq_in_index AS ordinal, column_name
            FROM information_schema.statistics
            WHERE non_unique=0 AND index_type IN ('BTREE','HASH')
              AND table_schema NOT IN ('information_schema','memsql','cluster','mysql','performance_schema','sys')
            """;
        protected override string IndexesSql => """
            SELECT table_schema AS database_name, table_name, index_name, seq_in_index AS ordinal,
              column_name, non_unique=0 AS unique_index,
              CASE WHEN index_type='BTREE' AND collation IN ('A','D') THEN collation='D' END AS descending,
              index_type<>'BTREE' OR collation IS NULL OR collation NOT IN ('A','D') AS unsupported
            FROM information_schema.statistics
            WHERE index_name<>'PRIMARY' AND non_unique=1 AND index_type NOT IN ('SHARD','COLUMNSTORE')
              AND table_schema NOT IN ('information_schema','memsql','cluster','mysql','performance_schema','sys')
            """;
        protected override string ViewsSql => """
            SELECT v.table_schema AS database_name, v.table_name, v.view_definition AS definition,
              c.column_name, c.ordinal_position AS ordinal, c.data_type AS type_name, c.is_nullable AS nullable,
              CASE WHEN c.character_maximum_length<=2147483647 THEN c.character_maximum_length END AS length, c.numeric_precision AS `precision`, c.numeric_scale AS `scale`
            FROM information_schema.views v LEFT JOIN information_schema.columns c
              ON c.table_schema=v.table_schema AND c.table_name=v.table_name
            WHERE v.table_schema NOT IN ('information_schema','memsql','cluster','mysql','performance_schema','sys')
            """;

        protected override async Task<List<CatalogRow>> ReadCatalogRowsAsync(DbConnection connection, string sql, DiscoveryOptions options, CancellationToken cancellationToken)
        {
            var rows = await base.ReadCatalogRowsAsync(connection, sql, options, cancellationToken).ConfigureAwait(false);
            if (sql == ViewsSql) return rows.Select(row => row.With(("definition", NormalizeViewDefinition(row.Text("definition"))))).ToList();
            if (sql != ColumnsSql) return rows;
            return rows.Select(NormalizeDefault).ToList();
        }

        private static readonly HashSet<string> LiteralDefaultTypes = new(StringComparer.Ordinal)
            { "char", "varchar", "text", "tinytext", "mediumtext", "longtext", "date", "time", "datetime", "timestamp" };

        private static CatalogRow NormalizeDefault(CatalogRow row)
        {
            // COLUMN_DEFAULT contains unquoted character values, unlike expression-oriented catalogs.
            if (row.Text("default_expression") is not { } value || !LiteralDefaultTypes.Contains(row.Text("type_name") ?? "")) return row;
            var temporal = row.Text("type_name") is "datetime" or "timestamp";
            if (temporal && value.StartsWith("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase)) return row;
            return row.With(("default_expression", "'" + value.Replace("'", "''") + "'"));
        }
        protected override MetadataColumn MapColumn(CatalogRow row)
        {
            var column = base.MapColumn(row);
            // Do not narrow unsigned ranges into signed portable columns.
            if (row.Text("column_type")?.Contains("unsigned", StringComparison.OrdinalIgnoreCase) == true)
                return column with { Type = null };
            return row.Text("type_name") switch
            {
                "json" => column with { Type = DbType.String, NativeType = "JSON", NativeDialect = typeof(SingleStoreDialect) },
                "tinytext" or "mediumtext" or "longtext" => column with { Type = DbType.String, Length = null },
                _ => column
            };
        }
    }
}
