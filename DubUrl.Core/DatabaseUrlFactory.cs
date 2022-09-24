﻿using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
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
    public class DatabaseUrlFactory
    {
        private ConnectionUrlFactory ConnectionUrlFactory { get; }
        private CommandProvisionerFactory CommandProvisionerFactory { get; }

        public DatabaseUrlFactory(ConnectionUrlFactory connectionUrlFactory, CommandProvisionerFactory commandProvisionerFactory)
            => (ConnectionUrlFactory, CommandProvisionerFactory) = (connectionUrlFactory, commandProvisionerFactory);

        public DatabaseUrl Instantiate(string url)
        {
            var connectionUrl = ConnectionUrlFactory.Instantiate(url);
            return new DatabaseUrl(connectionUrl, CommandProvisionerFactory);
        }
    }
}