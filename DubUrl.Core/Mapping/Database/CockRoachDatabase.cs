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
    [Database<PgsqlDialect>(
        "CockRoachDB"
        , new[] { "cr", "cockroach", "crdb", "cdb" }
    )]
    internal class CockRoachDatabase : IDatabase { }
}
