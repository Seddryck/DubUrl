using DubUrl.Mapping.Database;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<MySqlDatabase, NamedParametrizer>(
        "MySqlConnector"
    )]
    internal class MySqlConnectorMapper : BaseMapper
    {
        public MySqlConnectorMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new MySqlConnectorRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }

        protected MySqlConnectorMapper(ConnectionStringRewriter rewriter, IDialect dialect, IParametrizer parametrizer)
            : base(rewriter, dialect, parametrizer) { }
    }
}
