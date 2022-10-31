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
    [Mapper<TeradataDatabase, PositionalParametrizer>(
        "Teradata.Client"
    )]
    internal class TeradataMapper : AdoNetProviderMapper
    {
        public TeradataMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new TeradataRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }
    }
}
