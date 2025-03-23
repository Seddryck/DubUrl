﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.BulkCopy.Engines;
using DubUrl.Querying.Dialects;

namespace DubUrl.BulkCopy;
public class BulkCopyEngineFactory
{
    public IBulkCopyEngine Create(ConnectionUrl connectionUrl)
        => connectionUrl.Dialect switch
        {
            DuckdbDialect _ => new DuckDbBulkCopyEngine(connectionUrl),
            TSqlDialect _ => new MsSqlServerBulkCopyEngine(connectionUrl),
            _ => throw new NotSupportedException($"Dialect '{connectionUrl.Dialect}' not supported")
        };
}
