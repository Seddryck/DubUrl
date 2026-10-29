using DubUrl.Mapping;
using DubUrl.Provider.MySql.Metadata;
using DubUrl.Provider.MySql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Mapping;

[Mapper<MySqlDatabase, NamedParametrizer>(
    "MySqlConnector"
)]
[ConnectionConfiguration(ApplicationName = "Application Name", Pooling = "Pooling", MinPoolSize = "Minimum Pool Size", MaxPoolSize = "Maximum Pool Size", ConnectionTimeout = "Connection Timeout", CommandTimeout = "Default Command Timeout")]
public class MySqlConnectorMapper : BaseMapper
{
    public MySqlConnectorMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new MySqlConnectorRewriter(csb),
              dialect,
              parametrizer
        )
    { }

    protected MySqlConnectorMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
        : base(rewriter, dialect, parametrizer) { }
}
