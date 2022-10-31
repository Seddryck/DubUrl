using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<SnowflakeDatabase, PositionalNamedParametrizer>(
        "Snowflake.Data"
    )]
    internal class SnowflakeMapper : AdoNetProviderMapper
    {
        public SnowflakeMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new SnowflakeRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }
    }
}
