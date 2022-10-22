using DubUrl.Mapping;
using DubUrl.MicroOrm;
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

namespace DubUrl.MicroOrm
{
    public class DatabaseUrlFactory : DubUrl.DatabaseUrlFactory
    {
        private IReflectionCache ReflectionCache { get; }

        public DatabaseUrlFactory(ConnectionUrlFactory connectionUrlFactory, CommandProvisionerFactory commandProvisionerFactory, IReflectionCache reflectionCache)
            : base(connectionUrlFactory, commandProvisionerFactory)
        { ReflectionCache = reflectionCache; }

        public override DatabaseUrl Instantiate(string url)
        {
            var connectionUrl = ConnectionUrlFactory.Instantiate(url);
            return new DatabaseUrl(connectionUrl, CommandProvisionerFactory, ReflectionCache);
        }
    }
}