using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<Db2Dialect>(
    "IBM DB2"
    , ["db2"]
    , DatabaseCategory.LargePlayer
)]
[Brand("ibm", "#052FAD")]
public class Db2Database : IDatabase
{ }