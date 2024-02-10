using DubUrl.Mapping.Database;
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

[Mapper<CrateDbDatabase, NamedParametrizer>("Npgsql")]
public class CrateDbMapper : BaseMapper
{
    public CrateDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new CrateDbRewriter(csb), 
              dialect,
              parametrizer
        )
    { }
}
