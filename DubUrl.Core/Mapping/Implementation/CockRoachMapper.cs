using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper(
        "CockRoachDB"
        , new[] { "cr", "cockroach", "crdb", "cdb" }
        , "Npgsql"
    )]
    internal class CockRoachMapper : PgsqlMapper
    {
        public CockRoachMapper(DbConnectionStringBuilder csb)
            : base(csb)
        => ReplaceTokenMapper(typeof(PgsqlMapper.DatabaseMapper), new DatabaseMapper());

        internal new class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, $"{urlInfo.Segments.First()}.bank");
                else
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
