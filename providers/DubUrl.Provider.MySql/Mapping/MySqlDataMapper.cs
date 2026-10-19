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

[AlternativeMapper<MySqlDatabase, NamedParametrizer>(
    "MySql.Data"
)]
public class MySqlDataMapper : MySqlConnectorMapper
{
    public MySqlDataMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new MySqlDataRewriter(csb), 
              dialect,
              parametrizer
        )
    { }
}
