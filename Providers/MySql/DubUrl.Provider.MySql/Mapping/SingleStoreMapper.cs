using DubUrl.Mapping;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Provider.MySql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Mapping;

[Mapper<SingleStoreDatabase, NamedParametrizer>(
    "SingleStoreConnector"
)]
[ConnectionConfiguration(ApplicationName = "Application Name", Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Connection Timeout", CommandTimeout = "Default Command Timeout", IdleTimeout = "Connection Idle Timeout", MaxLifetime = "Connection Lifetime", Tls = "MySql")]
public class SingleStoreMapper : BaseMapper
{
    public SingleStoreMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new SingleStoreRewriter(csb), 
              dialect,
              parametrizer
        )
    { }
}
