using DubUrl.Mapping;
using DubUrl.Provider.Trino.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Trino.Metadata;

[Database<TrinoDialect>(
    "Trino"
    , ["tr", "trino"]
    , DatabaseCategory.DistributedQueryEngine
)]
[Brand("trino", "#DD00A1")]
public class TrinoDatabase : IDatabase
{ }