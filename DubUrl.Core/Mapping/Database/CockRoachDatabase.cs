using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<CockRoachDialect>(
    "CockRoachDB"
    , new[] { "cr", "cockroach", "cockroachdb", "crdb", "cdb" }
    , DatabaseCategory.Warehouse
)]
[Brand("cockroachlabs", "#6933FF")]
public class CockRoachDatabase : IDatabase { }
