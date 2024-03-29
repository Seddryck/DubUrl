﻿using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<CrateDbDialect>(
    "CrateDB"
    , ["crt", "crate", "cratedb"]
    , DatabaseCategory.Warehouse
)]
[Brand("cratedb", "#009DC7")]
public class CrateDbDatabase : IDatabase
{ }
