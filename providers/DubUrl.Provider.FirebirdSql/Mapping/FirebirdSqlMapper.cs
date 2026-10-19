using DubUrl.Mapping;
using DubUrl.Provider.FirebirdSql.Metadata;
using DubUrl.Provider.FirebirdSql.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.FirebirdSql.Mapping;

[Mapper<FirebirdSqlDatabase, NamedParametrizer>("FirebirdSql.Data.FirebirdClient")]
public class FirebirdSqlMapper : BaseMapper, IFileBasedMapper
{
    public FirebirdSqlMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath)
        : base(new FirebirdSqlRewriter(csb, rootPath),
              dialect,
              parametrizer
        )
    { }
}
