using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using DubUrl.Provider.PostgreSql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Mapping;

[Mapper<CrateDbDatabase, NpgsqlNamedParametrizer>("Npgsql")]
[ConnectionConfiguration(Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Timeout", CommandTimeout = "Command Timeout", IdleTimeout = "Connection Idle Lifetime", MaxLifetime = "Connection Lifetime", Tls = "Npgsql")]
public class CrateDbMapper : BaseMapper
{
    public CrateDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new CrateDbRewriter(csb), 
              dialect,
              parametrizer
        )
    { }
}
