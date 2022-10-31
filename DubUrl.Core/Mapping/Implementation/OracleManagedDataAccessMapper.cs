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
    [Mapper<OracleDatabase, NamedParametrizer>(
        "Oracle.ManagedDataAccess"
    )]
    internal class OracleManagedDataAccessMapper : AdoNetProviderMapper
    {
        public OracleManagedDataAccessMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new OracleManagedDataAccessRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }
    }
}
