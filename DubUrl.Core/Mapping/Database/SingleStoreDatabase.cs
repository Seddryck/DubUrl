using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<SingleStoreDialect>(
    "SingleStore"
    , ["singlestore", "single"]
    , DatabaseCategory.Warehouse
)]
[Brand("singlestore", "#AA00FF")]
public class SingleStoreDatabase : IDatabase
{ }
