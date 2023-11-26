using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<QuestDbDialect>(
    "QuestDb"
    , new[] { "quest", "questdb" }
    , DatabaseCategory.TimeSeries
)]
public class QuestDbDatabase : IDatabase
{ }
