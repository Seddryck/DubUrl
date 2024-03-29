﻿using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<TeradataDialect>(
    "Teradata"
    , ["td", "teradata", "tera"]
    , DatabaseCategory.Warehouse
)]
[Brand("Teradata", "#F37440")]
public class TeradataDatabase : IDatabase
{ }