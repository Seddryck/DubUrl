using DubUrl.Parsing;
using DubUrl.Rewriting;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace DubUrl.Adomd.Rewriting
{
    internal class PowerBiPremiumRewriter : ConnectionStringRewriter
    {
        protected internal const string SERVER_KEYWORD = "Data Source";

        protected internal const string POWERBI_SCHEME = "powerbi";
        protected internal const string DEFAULT_POWERBI_HOST = "api.powerbi.com";
        protected internal const string DEFAULT_POWERBI_VERSION = "v1.0";
        protected internal const string DEFAULT_POWERBI_TENANT = "myorg";

        public PowerBiPremiumRewriter(DbConnectionStringBuilder csb)
            : base(new SpecificatorUnchecked(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper()
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            public override void Execute(UrlInfo urlInfo)
            {
                var fullHost = new StringBuilder();
                var segments = urlInfo.Segments.ToList();

                fullHost.Append(POWERBI_SCHEME).Append(Uri.SchemeDelimiter);


                if (urlInfo.Host.Equals(DEFAULT_POWERBI_HOST, StringComparison.InvariantCultureIgnoreCase)
                    || urlInfo.Host.Equals(string.Empty))
                    segments.Insert(0, DEFAULT_POWERBI_HOST);
                else
                    throw new ArgumentException($"The host of a Power BI uri must be '{DEFAULT_POWERBI_HOST}' or empty.");

                if (!urlInfo.Segments[0].Equals(DEFAULT_POWERBI_VERSION, StringComparison.InvariantCultureIgnoreCase))
                    segments.Insert(1, DEFAULT_POWERBI_VERSION);
                if (segments.Count==3)
                    segments.Insert(2, DEFAULT_POWERBI_TENANT);
                if (segments.Count != 4)
                    throw new ArgumentException($"Cannot map the uri '{string.Join('/', segments)}' to a Power BI data source");
                for (int i = 0; i < segments.Count; i++)
                    segments[i] = Encode(segments[i]);

                fullHost.AppendJoin('/', segments.ToArray());
               
                Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
            }

            protected virtual string Encode(string value)
                => Uri.UnescapeDataString(value).Length < value.Length
                    ? value
                    : Uri.EscapeDataString(value);
        }
    }
}
