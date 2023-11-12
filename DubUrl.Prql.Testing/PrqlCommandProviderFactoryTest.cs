using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Prql;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;

namespace DubUrl.Prql.Testing
{
    public class PrqlCommandProviderFactoryTest
    {
        
        [Test]
        public void Instantiate_PrqlCommand_InlinePrqlProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            factory.Add<InlinePrqlProvider>(PrqlCommand.Instance);

            var provider = factory.Instantiate("query", PrqlCommand.Instance);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider, Is.InstanceOf<InlinePrqlProvider>());
        }

        [Test]
        public void Instantiate_Default_InlinePrqlProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new PrqlCommandProviderFactory(logger);

            var provider = factory.Instantiate("query");
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider, Is.InstanceOf<InlinePrqlProvider>());
        }
    }
}
