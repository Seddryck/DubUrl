using DubUrl.Mapping;
using DubUrl.Provider.Snowflake.Metadata;
using DubUrl.Provider.Snowflake.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Snowflake.Mapping;

[Mapper<SnowflakeDatabase, PositionalNamedParametrizer>(
    "Snowflake.Data"
)]
public class SnowflakeMapper : BaseMapper
{
    public SnowflakeMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new SnowflakeRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
