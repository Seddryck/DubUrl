using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation
{
    internal class CockRoachRewriter : PostgresqlRewriter
    {
        public CockRoachRewriter(DbConnectionStringBuilder csb)
            : base(csb)
            => ReplaceTokenMapper(typeof(PostgresqlRewriter.DatabaseMapper), new DatabaseMapper());

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
