using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<PgsqlDialect>(
    "PostgreSQL"
    , ["pg", "pgx", "pgsql", "postgres", "postgresql"]
    , DatabaseCategory.TopPlayer
)]
[Brand("postgresql", "#4169E1")]
public class PostgresqlDatabase : IDatabase
{ }
