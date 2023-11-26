using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<DuckdbDialect>(
    "DuckDB"
    , new[] { "duck", "duckdb" }
    , DatabaseCategory.InMemory
)]
[Brand("duckdb", "#FFF000", "#000000")]
public class DuckdbDatabase : IDatabase
{ }
