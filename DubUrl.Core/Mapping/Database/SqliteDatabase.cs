﻿using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Database;

[Database<SqliteDialect>(
    "SQLite3"
    , ["sq", "sqlite"]
    , DatabaseCategory.InMemory
)]
[Brand("sqlite", "#003B57")]
public class SqliteDatabase : IDatabase
{ }