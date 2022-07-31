using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    [AlternativeMapper(typeof(MariaDbConnectorMapper))]
    [Mapper(
        "MariaDB"
        , new[] { "maria", "mariadb" }
        , "MySql.Data"
    )]
    internal class MariaDbDataMapper : MySqlDataMapper
    {
        public MariaDbDataMapper(DbConnectionStringBuilder csb)
            : base(csb)
        { }
    }
}
