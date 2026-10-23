using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using DubUrl.Provider.PostgreSql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Mapping;

[MapperAttribute<PostgresqlDatabase, NpgsqlParametrizer>(
    "Npgsql"
)]
[DubUrl.Discovery.SchemaDiscovery(typeof(DubUrl.Provider.PostgreSql.Discovery.PostgreSqlMetadataReader))]
public class PostgresqlMapper : BaseMapper
{
    public PostgresqlMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new PostgresqlRewriter(csb),
              dialect,
              parametrizer
        )
    { }

    protected PostgresqlMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        : base(rewriter,
              dialect,
              parametrizer
        )
    { }
}
