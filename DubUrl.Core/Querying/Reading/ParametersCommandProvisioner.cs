using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class ParametersCommandProvisioner : ICommandProvisioner
    {
        private IParametrizedCommand Provider { get; }
        private IParametrizer Parametrizer { get; }

        public ParametersCommandProvisioner(IParametrizedCommand provider, IParametrizer parametrizer)
            => (Provider, Parametrizer) = (provider, parametrizer);

        public void Execute(IDbCommand command)
        {
            foreach (var parameter in Provider.Parameters)
            {
                var param = Parametrizer.CreateParameter(command, parameter);
                command.Parameters.Add(param);
            }
        }
    }
}
