﻿using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation;

[Mapper<MsSqlServerDatabase, NamedParametrizer>(
    "Microsoft.Data.SqlClient"
)]
public class MsSqlServerMapper : BaseMapper
{
    public MsSqlServerMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new MsSqlServerRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
