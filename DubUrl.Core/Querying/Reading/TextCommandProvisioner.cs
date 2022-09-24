using DubUrl.Querying.Dialecting;
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

        public TextCommandProvisioner(ICommandProvider provider, IDialect dialect)
            => (Provider, Dialect) = (provider, dialect);

        public void Execute(IDbCommand command)
        {
            if (!Provider.Exists(Dialect, true))
                throw new MissingCommandForDialectException(Provider, Dialect);

            command.CommandType = CommandType.Text;
            command.CommandText = Provider.Read(Dialect);
        }
    }
}
