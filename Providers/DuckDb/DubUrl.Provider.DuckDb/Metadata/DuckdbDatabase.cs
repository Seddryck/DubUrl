using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Metadata;

[Database<DuckDbDialect>(
    "DuckDB"
    , ["duck", "duckdb"]
    , DatabaseCategory.InMemory
)]
[Brand("duckdb", "#FFF000", "#000000")]
public class DuckdbDatabase : IDatabase
{ }
