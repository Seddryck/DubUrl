using DubUrl.Mapping;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Mapping;

[Mapper<MariaDbDatabase, NamedParametrizer>("MySqlConnector")]
[ConnectionConfiguration(ApplicationName = "Application Name", Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Connection Timeout", CommandTimeout = "Default Command Timeout", IdleTimeout = "Connection Idle Timeout", MaxLifetime = "Connection Lifetime", Tls = "MySql", TimeZone = "sql:mysql")]
public class MariaDbConnectorMapper : MySqlConnectorMapper
{
    public MariaDbConnectorMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(csb, dialect, parametrizer)
    { }
}
