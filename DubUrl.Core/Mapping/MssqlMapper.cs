using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class MssqlMapper : BaseMapper
    {
        public MssqlMapper(DbConnectionStringBuilder csb) : base(csb) { }

        public override void ExecuteSpecific(UrlInfo urlInfo)
        {
            ExecuteDataSource(urlInfo.Host, urlInfo.Segments, urlInfo.Port);
            ExecuteAuthentification(urlInfo.Username, urlInfo.Password);
            ExecuteInitialCatalog(urlInfo.Segments);
        }

        protected internal void ExecuteDataSource(string host, string[] segments, int port)
        {
            var fullHost = new StringBuilder();
            fullHost.Append(host);
            if (segments.Length == 2)
                fullHost.Append('\\').Append(segments.First());
            if (port != 0)
                fullHost.Append(',').Append(port);

            Specify("Data source", fullHost.ToString());
        }

        protected internal void ExecuteAuthentification(string username, string password)
        {
            if (!string.IsNullOrEmpty(username))
                Specify("User id", username);
            if (!string.IsNullOrEmpty(password))
                Specify("Password", password);

            if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
                Specify("Integrated Security", "sspi");
            else
                Specify("Integrated Security", false);
        }

        protected internal void ExecuteInitialCatalog(string[] segments)
        {
            if (segments.Length > 0 && segments.Count() <= 2)
                Specify("Initial Catalog", segments.Last());
            else
                throw new ArgumentOutOfRangeException();
        }
    }
}
