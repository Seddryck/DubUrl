using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<VerticaDialect>(
    "Vertica"
    , ["ve", "vertica"]
    , DatabaseCategory.Analytics
)]
[Brand("vertica", "#0066ff")]
public class VerticaDatabase : IDatabase
{ }
