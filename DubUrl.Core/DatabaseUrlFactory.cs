using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class DatabaseUrlFactory
    {
        private ConnectionUrlFactory ConnectionUrlFactory { get; }
        protected CommandFactory CommandFactory { get; }

        public DatabaseUrlFactory(ConnectionUrlFactory connectionUrlFactory, CommandFactory commandFactory)
            => (ConnectionUrlFactory, CommandFactory) = (connectionUrlFactory, commandFactory);

        public DatabaseUrl Instantiate(string url)
        {
            var connectionUrl = ConnectionUrlFactory.Instantiate(url);
            return new DatabaseUrl(connectionUrl, CommandFactory);
        }
    }
}