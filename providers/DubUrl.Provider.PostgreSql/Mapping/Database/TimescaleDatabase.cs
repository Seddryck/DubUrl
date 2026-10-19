using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<PgsqlDialect>(
    "Timescale"
    , ["ts", "timescale"]
    , DatabaseCategory.TimeSeries
)]
[Brand("timescale", "#FDB515", "#000000")]
public class TimescaleDatabase : IDatabase
{ }
