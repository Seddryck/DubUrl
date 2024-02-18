using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class CockRoachRewriter : PostgresqlRewriter
{
    private const int DEFAULT_PORT = 26257;

    public CockRoachRewriter(DbConnectionStringBuilder csb)
        : base(csb)
        => Replace(typeof(PostgresqlRewriter.PortMapper), new PortMapper());

    internal new class PortMapper : PostgresqlRewriter.PortMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            base.Execute(urlInfo);
            if (urlInfo.Port == 0)
                Specificator.Execute(PORT_KEYWORD, DEFAULT_PORT);
        }
    }
}
