using DubUrl.Mapping;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Provider.MySql.Rewriting;
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

[AlternativeMapper<MariaDbDatabase, NamedParametrizer>("MySql.Data")]
[ConnectionConfiguration(Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Connection Timeout", CommandTimeout = "Default Command Timeout", MaxLifetime = "Connection Lifetime", Tls = "ConnectorNet", TimeZone = "sql:mysql")]
public class MariaDbDataRewriter : MySqlDataMapper
{
    public MariaDbDataRewriter(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(csb, dialect, parametrizer)
    { }
}
