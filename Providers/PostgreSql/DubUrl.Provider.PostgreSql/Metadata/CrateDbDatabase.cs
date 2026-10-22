using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Metadata;

[Database<CrateDbDialect>(
    "CrateDB"
    , ["crt", "crate", "cratedb"]
    , DatabaseCategory.Warehouse
)]
[Brand("cratedb", "#009DC7")]
public class CrateDbDatabase : IDatabase
{ }
