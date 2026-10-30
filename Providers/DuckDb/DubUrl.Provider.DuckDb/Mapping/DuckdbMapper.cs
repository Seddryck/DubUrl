using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Metadata;
using DubUrl.Provider.DuckDb.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Mapping;

[MapperAttribute<DuckdbDatabase, PositionalParametrizer>(
    "DuckDB.NET.Data"
)]
[DubUrl.BulkCopy.BulkCopyProvider("DubUrl.Provider.DuckDb.Native.BulkCopy", "DubUrl.Provider.DuckDb.Native.BulkCopy.DuckDbBulkCopyProvider")]
[DubUrl.Discovery.SchemaDiscovery(typeof(DubUrl.Provider.DuckDb.Discovery.DuckDbMetadataReader))]
[ReadOnlyConnection("access_mode", "READ_ONLY", "DataSource", "SELECT readonly FROM duckdb_databases() WHERE database_name = current_database()")]
[DubUrl.Authentication.FileSecretConnection("ENCRYPTION_KEY", "DubUrl.Provider.DuckDb.Native", "DubUrl.Provider.DuckDb.Native.EncryptedConnectionFactory")]
public class DuckdbMapper : BaseMapper, IFileBasedMapper
{
    public DuckdbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath)
        : this(new DuckdbRewriter(csb, rootPath),
              dialect,
              parametrizer
        )
    { }

    protected DuckdbMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        : base(rewriter,
              dialect,
              parametrizer
        )
    { }
}
