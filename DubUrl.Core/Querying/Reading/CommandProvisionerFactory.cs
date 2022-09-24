using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class CommandProvisionerFactory
    {
        public virtual ICommandProvisioner[] Instantiate(ICommandProvider provider, ConnectionUrl connectionUrl)
        {
            var list = new List<ICommandProvisioner>
            {
                new TextCommandProvisioner(provider, connectionUrl.Dialect)
            };
            if (provider is IParametrizedCommand parametrizedProvider)
                list.Add(new ParametersCommandProvisioner(parametrizedProvider, connectionUrl.Parametrizer));
            return list.ToArray();
        }
    }
}
