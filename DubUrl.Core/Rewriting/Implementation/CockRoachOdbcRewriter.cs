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


namespace DubUrl.Rewriting.Implementation
{
    public class CockRoachOdbcRewriter : OdbcRewriter, IOdbcConnectionStringRewriter
    {
        protected internal const string PORT_KEYWORD = "Port";

        public CockRoachOdbcRewriter(DbConnectionStringBuilder csb)
            : this(csb, new DriverLocatorFactory()) { }
        public CockRoachOdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
            : base(csb,
                  new BaseTokenMapper[] {
                    new HostMapper(),
                    new PortMapper(),
                    new AuthentificationMapper(),
                    new DriverMapper(driverLocatorFactory),
                    new OptionsMapper(),
                  }
            )
        { }

        protected internal new class HostMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
                => Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
        }
        internal class PortMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
                => Specificator.Execute(PORT_KEYWORD, urlInfo.Port > 0 ? urlInfo.Port : 26257);
        }
    }
}
