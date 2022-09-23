using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<Db2Database, NamedParametrizer>("IBM.Data.DB2.Core")]
    internal class Db2Mapper : BaseMapper
    {
        public Db2Mapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new Db2Rewriter(csb),
                  dialect,
                  parametrizer
            )
        { }
    }
}
