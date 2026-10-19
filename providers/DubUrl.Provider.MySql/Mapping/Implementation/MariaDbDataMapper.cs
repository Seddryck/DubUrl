using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation;

[AlternativeMapper<MariaDbDatabase, NamedParametrizer>("MySql.Data")]
public class MariaDbDataRewriter : MySqlDataMapper
{
    public MariaDbDataRewriter(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(csb, dialect, parametrizer)
    { }
}
