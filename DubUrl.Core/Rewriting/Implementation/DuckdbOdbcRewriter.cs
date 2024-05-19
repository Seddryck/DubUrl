using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.Options;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DubUrl.Rewriting.Implementation;

public class DuckdbOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
{
    public DuckdbOdbcRewriter(DbConnectionStringBuilder csb)
        : this(csb, new DriverLocatorFactory()) { }
    public DuckdbOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : base(csb,
              [
                new HostMapper(),
                new DatabaseMapper(),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory),
                new OptionsMapper(),
              ]
        )
    { }

    protected internal new class HostMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Host))
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
        }
    }
}
