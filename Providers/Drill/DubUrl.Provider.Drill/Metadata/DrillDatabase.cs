using DubUrl.Mapping;
using DubUrl.Provider.Drill.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Drill.Metadata;

[Database<DrillDialect>(
    "Apache Drill"
    , ["drill"]
    , DatabaseCategory.DistributedQueryEngine
)]
public class DrillDatabase : IDatabase
{ }