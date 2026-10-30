using DubUrl.Mapping;
using DubUrl.Provider.Db2.Metadata;
using DubUrl.Provider.Db2.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Db2.Mapping;

[Mapper<Db2Database, NamedParametrizer>("IBM.Data.Db2")]
[ConnectionConfiguration(ApplicationName = "ClientApplicationName", ConnectionTimeout = "Connect Timeout")]
public class Db2Mapper : BaseMapper
{
    public Db2Mapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new Db2Rewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
