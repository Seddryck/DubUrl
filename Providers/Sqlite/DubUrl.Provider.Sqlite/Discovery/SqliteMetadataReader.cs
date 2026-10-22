using DubUrl.Discovery;

namespace DubUrl.Provider.Sqlite.Discovery;

/// <summary>SQLite main-database metadata via read-only table-valued PRAGMAs.</summary>
public sealed class SqliteMetadataReader : CatalogSchemaMetadataReader
{
    public override void ConfigureOwnedConnection(System.Data.Common.DbConnection connection)
    {
        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        builder["Mode"] = "ReadOnly";
        connection.ConnectionString = builder.ConnectionString;
    }

    protected override IReadOnlySet<SchemaFeature> UnsupportedFeatures => new HashSet<SchemaFeature>
        { SchemaFeature.Checks, SchemaFeature.Comments };
    protected override string TablesSql => """
        SELECT 'main' AS schema_name,name AS table_name FROM main.sqlite_schema
        WHERE type='table' AND name NOT LIKE 'sqlite_%'
        """;
    protected override string ColumnsSql => """
        SELECT 'main' AS schema_name,t.name AS table_name,p.name AS column_name,p.cid AS ordinal,p.type AS type_name,
          CASE WHEN p."notnull"=1 OR (p.pk>0 AND upper(p.type)='INTEGER' AND
            (SELECT count(*) FROM pragma_table_xinfo(t.name) x WHERE x.pk>0)=1) THEN 0 ELSE 1 END AS nullable,
          p.dflt_value AS default_expression,
          CASE WHEN p.hidden IN (2,3) THEN '<unavailable generated expression>' END AS generation_expression,
          CASE WHEN p.hidden=3 THEN 1 ELSE 0 END AS stored
        FROM main.sqlite_schema t,pragma_table_xinfo(t.name) p
        WHERE t.type='table' AND t.name NOT LIKE 'sqlite_%'
        """;
    protected override string KeysSql => """
        SELECT 'main' AS schema_name,t.name AS table_name,NULL AS key_name,'PRIMARY KEY' AS kind,p.pk AS ordinal,p.name AS column_name,
          NULL AS target_schema,NULL AS target_table,NULL AS target_column
        FROM main.sqlite_schema t,pragma_table_xinfo(t.name) p WHERE t.type='table' AND t.name NOT LIKE 'sqlite_%' AND p.pk>0
        UNION ALL
        SELECT 'main',t.name,'fk_'||t.name||'_'||f.id,'FOREIGN KEY',f.seq,f."from",'main',f."table",
          COALESCE(f."to",(SELECT x.name FROM pragma_table_xinfo(f."table") x WHERE x.pk=f.seq+1))
        FROM main.sqlite_schema t,pragma_foreign_key_list(t.name) f WHERE t.type='table' AND t.name NOT LIKE 'sqlite_%'
        UNION ALL
        SELECT 'main',t.name,i.name,'UNIQUE',x.seqno,x.name,NULL,NULL,NULL
        FROM main.sqlite_schema t,pragma_index_list(t.name) i,pragma_index_xinfo(i.name) x
        WHERE t.type='table' AND t.name NOT LIKE 'sqlite_%' AND i.origin='u' AND x."key"=1
        """;
    protected override string IndexesSql => """
        SELECT 'main' AS schema_name,t.name AS table_name,i.name AS index_name,i."unique" AS unique_index,
          x.seqno AS ordinal,x.name AS column_name,x.desc AS descending,
          CASE WHEN i.partial=1 OR x.cid<0 THEN 1 ELSE 0 END AS unsupported
        FROM main.sqlite_schema t,pragma_index_list(t.name) i,pragma_index_xinfo(i.name) x
        WHERE t.type='table' AND t.name NOT LIKE 'sqlite_%' AND i.origin='c' AND x."key"=1
        """;
    protected override string ViewsSql => """
        SELECT 'main' AS schema_name,v.name AS table_name,v.sql AS definition,p.name AS column_name,
          p.cid AS ordinal,p.type AS type_name,CASE WHEN p."notnull"=1 THEN 0 ELSE 1 END AS nullable
        FROM main.sqlite_schema v LEFT JOIN pragma_table_xinfo(v.name) p WHERE v.type='view'
        """;
}
