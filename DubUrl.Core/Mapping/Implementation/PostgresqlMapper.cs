using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Implementation;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [MapperAttribute<PostgresqlDatabase, PositionalParametrizer>(
        "Npgsql"
    )]
    internal class PostgresqlMapper : BaseMapper
    {
        public PostgresqlMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new PostgresqlRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }

        protected PostgresqlMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
            : base(rewriter,
                  dialect,
                  parametrizer
            )
        { }
    }
}
