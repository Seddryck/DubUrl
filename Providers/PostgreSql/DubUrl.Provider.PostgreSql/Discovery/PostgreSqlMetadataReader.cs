using DubUrl.Discovery;

namespace DubUrl.Provider.PostgreSql.Discovery;

/// <summary>PostgreSQL catalog metadata. PostgreSQL-compatible protocols are not automatically opted in.</summary>
public sealed class PostgreSqlMetadataReader : CatalogSchemaMetadataReader
{
    protected override string TablesSql => """
        SELECT n.nspname AS schema_name, c.relname AS table_name, obj_description(c.oid) AS comment
        FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
        WHERE c.relkind IN ('r','p') AND n.nspname NOT IN ('pg_catalog','information_schema')
          AND n.nspname NOT LIKE 'pg_toast%' AND n.nspname NOT LIKE 'pg_temp_%'
        """;
    protected override string ColumnsSql => """
        SELECT c.table_schema AS schema_name,c.table_name,c.column_name,c.ordinal_position AS ordinal,
          c.data_type AS type_name,c.udt_name AS native_type_name,c.is_nullable AS nullable,c.character_maximum_length AS length,
          c.numeric_precision AS precision,c.numeric_scale AS scale,c.column_default AS default_expression,
          CASE WHEN c.is_identity='YES' THEN c.identity_start END AS identity_seed,
          CASE WHEN c.is_identity='YES' THEN c.identity_increment END AS identity_increment,
          CASE WHEN c.is_generated='ALWAYS' THEN c.generation_expression END AS generation_expression,
          TRUE AS stored, col_description(pc.oid,a.attnum) AS comment
        FROM information_schema.columns c
        JOIN pg_catalog.pg_namespace n ON n.nspname=c.table_schema
        JOIN pg_catalog.pg_class pc ON pc.relnamespace=n.oid AND pc.relname=c.table_name
        JOIN pg_catalog.pg_attribute a ON a.attrelid=pc.oid AND a.attname=c.column_name
        WHERE c.table_schema NOT IN ('pg_catalog','information_schema') AND pc.relkind IN ('r','p')
        """;
    protected override string KeysSql => """
        SELECT n.nspname AS schema_name,t.relname AS table_name,c.conname AS key_name,
          CASE c.contype WHEN 'p' THEN 'PRIMARY KEY' WHEN 'u' THEN 'UNIQUE' ELSE 'FOREIGN KEY' END AS kind,
          k.ordinality AS ordinal,a.attname AS column_name,tn.nspname AS target_schema,
          tt.relname AS target_table,ta.attname AS target_column
        FROM pg_catalog.pg_constraint c
        JOIN pg_catalog.pg_class t ON t.oid=c.conrelid JOIN pg_catalog.pg_namespace n ON n.oid=t.relnamespace
        CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY k(attnum,ordinality)
        JOIN pg_catalog.pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.attnum
        LEFT JOIN pg_catalog.pg_class tt ON tt.oid=c.confrelid
        LEFT JOIN pg_catalog.pg_namespace tn ON tn.oid=tt.relnamespace
        LEFT JOIN pg_catalog.pg_attribute ta ON ta.attrelid=tt.oid AND ta.attnum=c.confkey[k.ordinality::int]
        WHERE c.contype IN ('p','u','f') AND n.nspname NOT IN ('pg_catalog','information_schema')
        """;
    protected override string IndexesSql => """
        SELECT n.nspname AS schema_name,t.relname AS table_name,ix.relname AS index_name,
          i.indisunique AS unique_index,k.ordinality AS ordinal,a.attname AS column_name,
          (i.indoption[(k.ordinality-1)::int] & 1)=1 AS descending,
          (i.indpred IS NOT NULL OR i.indexprs IS NOT NULL OR i.indnatts<>i.indnkeyatts OR am.amname<>'btree') AS unsupported
        FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class t ON t.oid=i.indrelid
        JOIN pg_catalog.pg_namespace n ON n.oid=t.relnamespace JOIN pg_catalog.pg_class ix ON ix.oid=i.indexrelid
        JOIN pg_catalog.pg_am am ON am.oid=ix.relam
        CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum,ordinality)
        LEFT JOIN pg_catalog.pg_attribute a ON a.attrelid=t.oid AND a.attnum=k.attnum
        WHERE NOT EXISTS (SELECT 1 FROM pg_catalog.pg_constraint c WHERE c.conindid=i.indexrelid AND c.contype IN ('p','u'))
          AND n.nspname NOT IN ('pg_catalog','information_schema')
        """;
    protected override string ChecksSql => """
        SELECT n.nspname AS schema_name,t.relname AS table_name,c.conname AS check_name,
          pg_get_expr(c.conbin,c.conrelid) AS expression
        FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_class t ON t.oid=c.conrelid
        JOIN pg_catalog.pg_namespace n ON n.oid=t.relnamespace
        WHERE c.contype='c' AND n.nspname NOT IN ('pg_catalog','information_schema')
        """;
    protected override string ViewsSql => """
        SELECT v.schemaname AS schema_name,v.viewname AS table_name,v.definition,
          c.column_name,c.ordinal_position AS ordinal,c.data_type AS type_name,c.is_nullable AS nullable
        FROM pg_catalog.pg_views v LEFT JOIN information_schema.columns c
          ON c.table_schema=v.schemaname AND c.table_name=v.viewname
        WHERE v.schemaname NOT IN ('pg_catalog','information_schema')
        """;
    protected override MetadataColumn MapColumn(CatalogRow row)
    {
        var column = base.MapColumn(row);
        if (row.Text("native_type_name") is "json" or "jsonb" or "citext")
            return column with { Type = System.Data.DbType.String, NativeType = row.Text("native_type_name"), NativeDialect = typeof(DubUrl.Provider.PostgreSql.Sql.PgsqlDialect) };
        return column.Type is System.Data.DbType.AnsiString ? column with { Type = System.Data.DbType.String } : column;
    }
}
