using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class OracleMapper : BaseMapper
    {
        public OracleMapper(DbConnectionStringBuilder csb) : base(csb) { }

        public override void ExecuteSpecific(UrlInfo urlInfo)
        {
            ExecuteDataSource(urlInfo.Host, urlInfo.Port, urlInfo.Segments);
            ExecuteAuthentification(urlInfo.Username, urlInfo.Password);
        }

        protected internal void ExecuteAuthentification(string username, string password)
        {
            if (!string.IsNullOrEmpty(username))
            {
                Specify("USER ID", username);
                if (!string.IsNullOrEmpty(password))
                    Specify("PASSWORD", password);
            }
            else
            {
                Specify("USER ID", "/");
                Specify("PASSWORD", string.Empty);
            }
        }

        protected internal void ExecuteDataSource(string host, int port, string[] segments)
        {
            //If only host is specified, it's the TNS name
            if (segments.Length == 0 && port == 0)
                Specify("DATA SOURCE", host);

            //If segment is specified then it's the ConnectDescriptor
            else if (segments.Length == 1)
                Specify("DATA SOURCE",
                    $"(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)" +
                    $"(HOST={host})(PORT={(port > 0 ? port : 1521)}))(CONNECT_DATA=" +
                    $"(SERVICE_NAME={segments.First()})))");
            else
                throw new ArgumentOutOfRangeException();
        }
    }
}
