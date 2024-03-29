﻿using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<MySqlDialect>(
    "MySQL"
    , ["mysql", "my"]
    , DatabaseCategory.TopPlayer
)]
[Brand("mysql", "#4479A1")]
public class MySqlDatabase : IDatabase
{ }