using DubUrl.Mapping.Database;
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
    [AlternativeMapper<MariaDbDatabase>("MySql.Data")]
    internal class MariaDbDataMapper : MySqlDataMapper
    {
        public MariaDbDataMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb, dialect)
        { }
    }
}
