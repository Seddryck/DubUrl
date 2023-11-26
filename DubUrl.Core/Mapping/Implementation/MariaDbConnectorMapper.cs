using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation;

[Mapper<MariaDbDatabase, NamedParametrizer>("MySqlConnector")]
internal class MariaDbConnectorMapper : MySqlConnectorMapper
{
    public MariaDbConnectorMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(csb, dialect, parametrizer)
    { }
}
