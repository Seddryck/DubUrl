﻿using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<MysqlDialect>(
        "MariaDB"
        , new[] { "maria", "mariadb" }
        , "MySqlConnector", 3
    )]
    internal class MariaDbConnectorMapper : MySqlConnectorMapper
    {
        public MariaDbConnectorMapper(DbConnectionStringBuilder csb)
            : base(csb)
        { }
    }
}
