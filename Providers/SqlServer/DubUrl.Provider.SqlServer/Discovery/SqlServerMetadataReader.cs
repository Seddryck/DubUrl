using DubUrl.Discovery;

namespace DubUrl.Provider.SqlServer.Discovery;

public sealed class SqlServerMetadataReader : CatalogSchemaMetadataReader
{
    protected override MetadataColumn MapColumn(CatalogRow row)
    {
        var column = base.MapColumn(row);
        return row.Text("type_name") is "timestamp" or "rowversion" ? column with { Type = System.Data.DbType.Binary, Length = 8 } : column;
    }

    protected override string TablesSql => """
        SELECT s.name AS schema_name,t.name AS table_name,CONVERT(nvarchar(max),ep.value) AS comment
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
        LEFT JOIN sys.extended_properties ep ON ep.major_id=t.object_id AND ep.minor_id=0 AND ep.name='MS_Description' AND ep.class=1
        WHERE t.is_ms_shipped=0
        """;
    protected override string ColumnsSql => """
        SELECT s.name AS schema_name,t.name AS table_name,c.name AS column_name,c.column_id AS ordinal,
          ty.name AS type_name,c.is_nullable AS nullable,
          CASE WHEN ty.name IN ('nvarchar','nchar') AND c.max_length>0 THEN c.max_length/2 ELSE c.max_length END AS length,
          c.precision,c.scale,dc.definition AS default_expression,
          CONVERT(bigint,ic.seed_value) AS identity_seed,CONVERT(bigint,ic.increment_value) AS identity_increment,
          cc.definition AS generation_expression,cc.is_persisted AS stored,CONVERT(nvarchar(max),ep.value) AS comment
        FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
        JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
        LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id
        LEFT JOIN sys.identity_columns ic ON ic.object_id=t.object_id AND ic.column_id=c.column_id
        LEFT JOIN sys.computed_columns cc ON cc.object_id=t.object_id AND cc.column_id=c.column_id
        LEFT JOIN sys.extended_properties ep ON ep.major_id=t.object_id AND ep.minor_id=c.column_id AND ep.name='MS_Description' AND ep.class=1
        WHERE t.is_ms_shipped=0
        """;
    protected override string KeysSql => """
        SELECT s.name AS schema_name,t.name AS table_name,k.name AS key_name,
          CASE k.type WHEN 'PK' THEN 'PRIMARY KEY' ELSE 'UNIQUE' END AS kind,
          ic.key_ordinal AS ordinal,c.name AS column_name,
          CAST(NULL AS nvarchar(128)) AS target_schema,CAST(NULL AS nvarchar(128)) AS target_table,CAST(NULL AS nvarchar(128)) AS target_column
        FROM sys.key_constraints k JOIN sys.tables t ON t.object_id=k.parent_object_id
        JOIN sys.schemas s ON s.schema_id=t.schema_id
        JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=k.unique_index_id
        JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
        WHERE t.is_ms_shipped=0 AND ic.key_ordinal>0
        UNION ALL
        SELECT s.name,t.name,f.name,'FOREIGN KEY',fc.constraint_column_id,c.name,ts.name,tt.name,tc.name
        FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id=f.parent_object_id
        JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
        JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=fc.parent_column_id
        JOIN sys.tables tt ON tt.object_id=f.referenced_object_id JOIN sys.schemas ts ON ts.schema_id=tt.schema_id
        JOIN sys.columns tc ON tc.object_id=tt.object_id AND tc.column_id=fc.referenced_column_id
        WHERE t.is_ms_shipped=0
        """;
    protected override string IndexesSql => """
        SELECT s.name AS schema_name,t.name AS table_name,i.name AS index_name,i.is_unique AS unique_index,
          ic.key_ordinal AS ordinal,c.name AS column_name,ic.is_descending_key AS descending,
          CASE WHEN i.has_filter=1 OR i.type NOT IN (1,2) OR EXISTS
            (SELECT 1 FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.is_included_column=1)
            THEN 1 ELSE 0 END AS unsupported
        FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id
        JOIN sys.index_columns ic ON ic.object_id=t.object_id AND ic.index_id=i.index_id
        JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=ic.column_id
        WHERE t.is_ms_shipped=0 AND i.is_primary_key=0 AND i.is_unique_constraint=0 AND i.index_id>0
        """;
    protected override string ChecksSql => """
        SELECT s.name AS schema_name,t.name AS table_name,c.name AS check_name,c.definition AS expression
        FROM sys.check_constraints c JOIN sys.tables t ON t.object_id=c.parent_object_id
        JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0
        """;
    protected override string ViewsSql => """
        SELECT s.name AS schema_name,v.name AS table_name,m.definition,c.name AS column_name,
          c.column_id AS ordinal,ty.name AS type_name,c.is_nullable AS nullable
        FROM sys.views v JOIN sys.schemas s ON s.schema_id=v.schema_id
        LEFT JOIN sys.sql_modules m ON m.object_id=v.object_id
        LEFT JOIN sys.columns c ON c.object_id=v.object_id LEFT JOIN sys.types ty ON ty.user_type_id=c.user_type_id
        WHERE v.is_ms_shipped=0
        """;
}
