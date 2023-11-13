using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying;

namespace DubUrl.Prql
{
    public class PrqlCommandProviderFactory : CommandProviderFactory
    {
        public PrqlCommandProviderFactory(IQueryLogger logger)
            : base(logger, PrqlCommand.Instance) { }

        protected override void Initialize()
        {
            Add<InlinePrqlProvider>(PrqlCommand.Instance);
        }
    }
}
