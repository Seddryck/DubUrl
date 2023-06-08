using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class DatabaseUrlFactory : IDatabaseUrlFactory
    {
        protected ConnectionUrlFactory ConnectionUrlFactory { get; }
        protected CommandProvisionerFactory CommandProvisionerFactory { get; }

        public DatabaseUrlFactory(ConnectionUrlFactory connectionUrlFactory, CommandProvisionerFactory commandProvisionerFactory)
            => (ConnectionUrlFactory, CommandProvisionerFactory) = (connectionUrlFactory, commandProvisionerFactory);

        public virtual DatabaseUrl Instantiate(string url)
        {
            var connectionUrl = ConnectionUrlFactory.Instantiate(url);
            return new DatabaseUrl(connectionUrl, CommandProvisionerFactory);
        }
    }
}