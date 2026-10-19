using DubUrl.Mapping;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Metadata;

[Database<MySqlDialect>(
    "MariaDB"
    , ["maria", "mariadb"]
    , DatabaseCategory.LargePlayer
)]
[Brand("mariadb", "#003545")]
public class MariaDbDatabase : IDatabase
{ }
