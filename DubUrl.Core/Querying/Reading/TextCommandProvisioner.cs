using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class TextCommandProvisioner : ICommandProvisioner
    {
        private ICommandProvider Provider { get; }
        private IDialect Dialect { get; }
        private IConnectivity Connectivity { get; }

        public TextCommandProvisioner(ICommandProvider provider, IDialect dialect, IConnectivity connectivity)
            => (Provider, Dialect, Connectivity) = (provider, dialect, connectivity);

        public void Execute(IDbCommand command)
        {
            if (!Provider.Exists(Dialect, Connectivity, true))
                throw new MissingCommandForDialectException(Provider, Dialect);

            command.CommandType = CommandType.Text;
            command.CommandText = Provider.Read(Dialect, Connectivity);
        }
    }
}
