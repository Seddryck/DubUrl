using DubUrl.Mapping;
using DubUrl.Provider.PostgreSql.Metadata;
using DubUrl.Provider.PostgreSql.Rewriting;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Mapping;

[Mapper<CockRoachDatabase, PositionalParametrizer>("Npgsql")]
public class CockRoachMapper : PostgresqlMapper
{
    public CockRoachMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new CockRoachRewriter(csb), dialect, parametrizer) { }
}
