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
    [Mapper<TrinoDatabase, NamedParametrizer>(
        "NReco.PrestoAdo"
    )]
    internal class TrinoMapper : BaseMapper
    {
        public TrinoMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new TrinoRewriter(csb),
                  dialect,
                  parametrizer
            )
        { }
    }
}