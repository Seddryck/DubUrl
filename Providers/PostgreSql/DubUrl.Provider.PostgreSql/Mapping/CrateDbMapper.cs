using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using DubUrl.Provider.PostgreSql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Mapping;

[Mapper<CrateDbDatabase, NpgsqlNamedParametrizer>("Npgsql")]
public class CrateDbMapper : BaseMapper
{
    public CrateDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new CrateDbRewriter(csb), 
              dialect,
              parametrizer
        )
    { }
}
