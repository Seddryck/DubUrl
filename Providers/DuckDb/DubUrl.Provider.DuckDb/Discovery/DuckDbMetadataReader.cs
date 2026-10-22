using DubUrl.Discovery;

namespace DubUrl.Provider.DuckDb.Discovery;

public sealed class DuckDbMetadataReader : CatalogSchemaMetadataReader
{
    protected override IReadOnlySet<SchemaFeature> UnsupportedFeatures => new HashSet<SchemaFeature>
        { SchemaFeature.Generation, SchemaFeature.Indexes };
    protected override string TablesSql => """
        SELECT schema_name,table_name,comment FROM duckdb_tables() WHERE NOT internal AND database_name=current_database()
        """;
    protected override string ColumnsSql => """
        SELECT c.table_schema AS schema_name,c.table_name,c.column_name,c.ordinal_position AS ordinal,
          c.data_type AS type_name,c.is_nullable AS nullable,c.character_maximum_length AS length,
          c.numeric_precision AS precision,c.numeric_scale AS scale,c.column_default AS default_expression,d.comment
        FROM information_schema.columns c JOIN duckdb_columns() d
          ON d.database_name=c.table_catalog AND d.schema_name=c.table_schema AND d.table_name=c.table_name AND d.column_name=c.column_name
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
