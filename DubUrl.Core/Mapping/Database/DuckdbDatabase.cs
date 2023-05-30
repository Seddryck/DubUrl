using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<DuckdbDialect>(
        "DuckDB"
        , new[] { "duck", "duckdb" }
        , DatabaseCategory.InMemory
    )]
    public class DuckdbDatabase : IDatabase
    { }
}
