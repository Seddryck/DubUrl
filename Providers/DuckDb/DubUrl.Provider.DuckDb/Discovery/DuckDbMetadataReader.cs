using DubUrl.Discovery;

namespace DubUrl.Provider.DuckDb.Discovery;

public sealed class DuckDbMetadataReader : CatalogSchemaMetadataReader
{

    protected override string TablesSql => """
        SELECT schema_name,table_name,comment FROM duckdb_tables() WHERE NOT internal AND database_name=current_database()
        """;
    protected override string ColumnsSql => """
        SELECT c.table_schema AS schema_name,c.table_name,c.column_name,c.ordinal_position AS ordinal,
          c.data_type AS type_name,c.is_nullable AS nullable,c.character_maximum_length AS length,
          c.numeric_precision AS precision,c.numeric_scale AS scale,c.column_default AS default_expression,d.comment,t.sql AS table_sql,t.table_oid
        FROM information_schema.columns c JOIN duckdb_columns() d
          ON d.database_name=c.table_catalog AND d.schema_name=c.table_schema AND d.table_name=c.table_name AND d.column_name=c.column_name
        JOIN duckdb_tables() t ON t.database_name=c.table_catalog AND t.schema_name=c.table_schema AND t.table_name=c.table_name
        WHERE c.table_catalog=current_database() AND NOT d.internal
        """;
    protected override string KeysSql => """
        SELECT c.schema_name,c.table_name,c.constraint_name AS key_name,c.constraint_type AS kind,
          n AS ordinal,c.constraint_column_names[n] AS column_name,
          CASE WHEN c.constraint_type='FOREIGN KEY' THEN c.schema_name END AS target_schema,
          c.referenced_table AS target_table,c.referenced_column_names[n] AS target_column
        FROM duckdb_constraints() c, LATERAL range(1,len(c.constraint_column_names)+1) ord(n)
        WHERE c.database_name=current_database() AND c.constraint_type IN ('PRIMARY KEY','UNIQUE','FOREIGN KEY')
        """;
    protected override string IndexesSql => """
        SELECT schema_name,table_name,index_name,is_unique AS unique_index,sql AS index_sql
        FROM duckdb_indexes() WHERE database_name=current_database()
        """;

    protected override async Task<List<CatalogRow>> ReadCatalogRowsAsync(System.Data.Common.DbConnection connection, string sql,
        DiscoveryOptions options, CancellationToken cancellationToken)
    {
        var rows = await base.ReadCatalogRowsAsync(connection, sql, options, cancellationToken).ConfigureAwait(false);
        if (sql == IndexesSql)
        {
            var indexes = new List<CatalogRow>();
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var columns = DuckDbCatalogSql.IndexColumns(row.Text("index_sql") ?? "");
                if (columns is null)
                {
                    indexes.Add(row.With(("unsupported", true)));
                    continue;
                }
                for (var i = 0; i < columns.Length; i++)
                    indexes.Add(row.With(("column_name", columns[i].Name), ("ordinal", i + 1), ("descending", columns[i].Descending)));
            }
            return indexes;
        }
        if (sql != ColumnsSql) return rows;
        var normalized = rows.Select(row =>
        {
            var expression = DuckDbCatalogSql.GeneratedExpression(row.Text("table_sql") ?? "", row.Text("column_name")!);
            return expression is null ? row : row.With(("generation_expression", expression), ("default_expression", null), ("stored", false));
        }).ToList();
        if (!options.Features.Contains(SchemaFeature.Generation)) return normalized;
        // Read definitions and dependencies only; never call nextval/currval or advance a sequence.
        var sequences = await QueryAsync(connection, """
            SELECT s.sequence_oid,s.database_name,s.schema_name,s.sequence_name,s.start_value,s.increment_by,
              s.min_value,s.max_value,s.cycle,d.refobjid AS dependent_oid
            FROM duckdb_sequences() s LEFT JOIN duckdb_dependencies() d ON d.objid=s.sequence_oid
            WHERE s.database_name=current_database()
            """, cancellationToken).ConfigureAwait(false);
        CatalogRow? Resolve(CatalogRow column)
        {
            if (!DuckDbCatalogSql.TrySequence(column.Text("default_expression") ?? "", out var name)) return null;
            var matches = sequences.Where(sequence =>
                sequence.Long("dependent_oid") == column.Long("table_oid")
                && string.Equals(sequence.Text("sequence_name"), name!.Name, StringComparison.OrdinalIgnoreCase)
                && (name.Schema is null || string.Equals(sequence.Text("schema_name"), name.Schema, StringComparison.OrdinalIgnoreCase))
                && (name.Database is null || string.Equals(sequence.Text("database_name"), name.Database, StringComparison.OrdinalIgnoreCase))
                && name.Catalog is null).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        var sequenceColumns = normalized.ToArray();
        var resolved = normalized.Select(Resolve).ToArray();
        for (var i = 0; i < normalized.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var column = normalized[i];
            var defaultSql = column.Text("default_expression");
            if (defaultSql is null || !DuckDbCatalogSql.UsesSequence(defaultSql)) continue;
            var sequence = resolved[i];
            var integral = base.MapColumn(column).Type is System.Data.DbType.Byte or System.Data.DbType.Int16 or System.Data.DbType.Int32 or System.Data.DbType.Int64;
            var increment = sequence?.Long("increment_by");
            var eligible = integral && sequence is not null && sequence.Bool("cycle") == false && increment is not null and not 0
                && (sequence.Long("min_value") == long.MinValue || (increment > 0 && sequence.Long("min_value") == 1))
                && (sequence.Long("max_value") == long.MaxValue || (increment < 0 && sequence.Long("max_value") == -1))
                && sequences.Where(item => item.Long("sequence_oid") == sequence.Long("sequence_oid"))
                    .All(item => item.Long("dependent_oid") == column.Long("table_oid"))
                && resolved.Count(item => item?.Long("sequence_oid") == sequence.Long("sequence_oid")) == 1
                && !sequenceColumns.Where((item, index) => resolved[index] is null && item.Long("table_oid") == column.Long("table_oid"))
                    .Any(item => DuckDbCatalogSql.UsesSequence(item.Text("default_expression") ?? ""))
                && !sequenceColumns.Where(item => item.Long("table_oid") == column.Long("table_oid"))
                    .Any(item => DuckDbCatalogSql.UsesSequence(item.Text("generation_expression") ?? ""));
            normalized[i] = eligible
                ? column.With(("identity_seed", sequence!.Long("start_value")), ("identity_increment", increment), ("default_expression", null))
                : column.With(("generation_expression", "<unrepresentable sequence-backed identity>"), ("default_expression", null));
        }
        return normalized;
    }

    protected override string ChecksSql => """
        SELECT schema_name,table_name,constraint_name AS check_name,expression FROM duckdb_constraints()
        WHERE constraint_type='CHECK' AND database_name=current_database()
        """;
    protected override string ViewsSql => """
        SELECT v.schema_name,v.view_name AS table_name,v.sql AS definition,c.column_name,
          c.ordinal_position AS ordinal,c.data_type AS type_name,c.is_nullable AS nullable
        FROM duckdb_views() v LEFT JOIN information_schema.columns c
          ON c.table_catalog=v.database_name AND c.table_schema=v.schema_name AND c.table_name=v.view_name
        WHERE NOT v.internal AND v.database_name=current_database()
        """;
    protected override MetadataColumn MapColumn(CatalogRow row)
    {
        var column = base.MapColumn(row);
        return column.Type == System.Data.DbType.AnsiString ? column with { Type = System.Data.DbType.String } : column;
    }
}
