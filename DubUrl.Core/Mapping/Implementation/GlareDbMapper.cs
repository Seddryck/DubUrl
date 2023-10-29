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

namespace DubUrl.Mapping.Implementation
{
    [Mapper<GlareDbDatabase, PositionalParametrizer>(
        "Npgsql"
    )]
    internal class GlareDbMapper : PostgresqlMapper
    {
        public GlareDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new GlareDbRewriter(csb), dialect, parametrizer)
        { }
    }
}
