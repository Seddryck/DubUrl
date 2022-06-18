using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class MySqlConnectorMapper : BaseMapper
    {
        public MySqlConnectorMapper(DbConnectionStringBuilder csb) : base(csb) { }

        public override void ExecuteSpecific(UrlInfo urlInfo)
        {
            Specify("Server", urlInfo.Host);
            if (urlInfo.Port>0)
                Specify("Port", urlInfo.Port);
            ExecuteAuthentification(urlInfo.Username, urlInfo.Password);
            ExecuteDatabase(urlInfo.Segments);
        }

        protected internal void ExecuteAuthentification(string username, string password)
        {
            if (!string.IsNullOrEmpty(username))
                Specify("User ID", username);
            if (!string.IsNullOrEmpty(password))
                Specify("Password", password);

            if (string.IsNullOrEmpty(username))
                throw new UsernameNotFoundException();
        }

        protected internal void ExecuteDatabase(string[] segments)
        {
            if (segments.Length == 1)
                Specify("Database", segments.First());
            else
                throw new ArgumentOutOfRangeException();
        }

        protected override void Specify(string keyword, object value)
        {
            if (ContainsKey(keyword))
                throw new InvalidOperationException($"The keyword '{keyword}' is already specified for this connection string.");
            if (value == null)
                throw new ArgumentNullException(nameof(value), $"The value for the keyword '{keyword}' cannot be null.");

            AddToken(keyword, value);
        }
    }
}
