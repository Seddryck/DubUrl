using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Implementation
{
    [Mapper<CockRoachDatabase>("Npgsql")]
    internal class CockRoachMapper : PostgresqlMapper
    {
        public CockRoachMapper(DbConnectionStringBuilder csb, IDialect dialect)
            : base(csb, dialect)
        => ReplaceTokenMapper(typeof(PostgresqlMapper.DatabaseMapper), new DatabaseMapper());

        internal new class DatabaseMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, $"{urlInfo.Segments.First()}.bank");
                else
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
