using DubUrl.Mapping.Tokening;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<PgsqlDialect>(
        "Timescale"
        , new[] { "ts", "timescale" }
        , "Npgsql", 6
    )]
    internal class TimescaleMapper : PgsqlMapper
    {
        public TimescaleMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb, dialect)
        { }
    }
}
