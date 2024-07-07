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
public class VerticaDatabase : IDatabase
{ }
