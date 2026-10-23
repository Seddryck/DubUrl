using DubUrl.Mapping;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Metadata;

[Database<SingleStoreDialect>(
    "SingleStore"
    , ["sg", "sgs", "singlestore", "single", "singlestoredb"]
    , DatabaseCategory.Warehouse
)]
[Brand("singlestore", "#AA00FF")]
public class SingleStoreDatabase : IDatabase
{ }
