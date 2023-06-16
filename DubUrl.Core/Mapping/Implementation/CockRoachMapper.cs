using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
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
    [Mapper<CockRoachDatabase, PositionalParametrizer>("Npgsql")]
    internal class CockRoachMapper : PostgresqlMapper
    {
        public CockRoachMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new CockRoachRewriter(csb), dialect, parametrizer) { }

    }
}
