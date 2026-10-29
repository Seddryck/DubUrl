using DubUrl.Mapping;
using DubUrl.Provider.Sqlite.Metadata;
using DubUrl.Provider.Sqlite.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Sqlite.Mapping;

[Mapper<SqliteDatabase, NamedParametrizer>(
    "Microsoft.Data.Sqlite"
)]
[DubUrl.Discovery.SchemaDiscovery(typeof(DubUrl.Provider.Sqlite.Discovery.SqliteMetadataReader))]
[ConnectionConfiguration(Pooling = "Pooling")]
public class SqliteMapper : BaseMapper, IFileBasedMapper
{
    public SqliteMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath)
        : base(new SqliteRewriter(csb, rootPath),
              dialect,
              parametrizer
        )
    { }
}
