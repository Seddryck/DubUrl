using DubUrl.Mapping;
using DubUrl.Provider.Oracle.Metadata;
using DubUrl.Provider.Oracle.Rewriting;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Oracle.Mapping;

[Mapper<OracleDatabase, OracleParametrizer>(
    "Oracle.ManagedDataAccess"
)]
public class OracleManagedDataAccessMapper : BaseMapper
{
    public OracleManagedDataAccessMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new OracleManagedDataAccessRewriter(csb),
              dialect,
              parametrizer
        )
    { }
}
