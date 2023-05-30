using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<CockRoachDialect>(
        "CockRoachDB"
        , new[] { "cr", "cockroach", "cockroachdb", "crdb", "cdb" }
        , DatabaseCategory.Warehouse
    )]
    public class CockRoachDatabase : IDatabase { }
}
