using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<SnowflakeDialect>(
    "Snowflake"
    , ["sf", "snowflake"]
    , DatabaseCategory.Warehouse
)]
[Brand("snowflake", "#29B5E8")]
public class SnowflakeDatabase : IDatabase
{ }