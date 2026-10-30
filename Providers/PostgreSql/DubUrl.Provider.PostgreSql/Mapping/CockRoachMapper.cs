using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using DubUrl.Provider.PostgreSql.Rewriting;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Mapping;

[Mapper<CockRoachDatabase, NpgsqlParametrizer>("Npgsql")]
[ConnectionConfiguration(ApplicationName = "Application Name", Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Timeout", CommandTimeout = "Command Timeout", IdleTimeout = "Connection Idle Lifetime", MaxLifetime = "Connection Lifetime", TimeZone = "Timezone", Tls = "Npgsql")]
public class CockRoachMapper : PostgresqlMapper
{
    public CockRoachMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new CockRoachRewriter(csb), dialect, parametrizer) { }
}
