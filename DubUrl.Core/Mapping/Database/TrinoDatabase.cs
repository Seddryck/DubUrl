using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database
{
    [Database<TrinoDialect>(
        "Trino"
        , new[] { "tr", "trino" }
        , DatabaseCategory.DistributedQueryEngine
    )]
    public class TrinoDatabase : IDatabase
    { }
}