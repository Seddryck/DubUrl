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

namespace DubUrl.Mapping.Implementation;

[Mapper<QuestDbDatabase, PositionalParametrizer>(
    "Npgsql"
)]
internal class QuestDbMapper : PostgresqlMapper
{
    public QuestDbMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
        : base(new QuestDbRewriter(csb), dialect, parametrizer)
    { }
}
