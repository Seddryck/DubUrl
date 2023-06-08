using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<PgsqlDialect>(
        "PostgreSQL"
        , new[] { "pg", "pgsql", "postgres", "postgresql" }
        , DatabaseCategory.TopPlayer
    )]
    public class PostgresqlDatabase : IDatabase
    { }
}
