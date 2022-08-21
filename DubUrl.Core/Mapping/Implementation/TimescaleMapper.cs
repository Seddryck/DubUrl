using DubUrl.Mapping.Database;
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
    [Mapper<TimescaleDatabase>(
        "Npgsql"
    )]
    internal class TimescaleMapper : PostgresqlMapper
    {
        public TimescaleMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb, dialect)
        { }
    }
}
