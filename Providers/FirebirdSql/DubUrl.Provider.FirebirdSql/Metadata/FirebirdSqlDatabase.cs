using DubUrl.Mapping;
using DubUrl.Provider.FirebirdSql.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.FirebirdSql.Metadata;

[Database<FirebirdSqlDialect>(
    "Firebird SQL"
    , ["fb", "firebird"]
    , DatabaseCategory.InMemory
)]
public class FirebirdSqlDatabase : IDatabase
{ }
