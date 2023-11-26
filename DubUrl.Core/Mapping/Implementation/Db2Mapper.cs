using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation;

[Mapper<Db2Database, NamedParametrizer>("IBM.Data.Db2")]
internal class Db2Mapper : BaseMapper
{
    public Db2Mapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new Db2Rewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
