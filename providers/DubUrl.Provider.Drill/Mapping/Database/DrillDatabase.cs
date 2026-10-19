using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<DrillDialect>(
    "Apache Drill"
    , ["drill"]
    , DatabaseCategory.DistributedQueryEngine
)]
public class DrillDatabase : IDatabase
{ }