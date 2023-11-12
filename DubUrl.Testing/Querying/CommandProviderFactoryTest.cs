using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing.Querying
{
    public class CommandProviderFactoryTest
    {
        [Test]
        public void GetInstantiator_CommandText_InlineSqlProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            var instantiator = factory.GetInstantiator<InlineSqlProvider>();
            Assert.That(instantiator, Is.Not.Null);
            Assert.That(instantiator.Invoke("query", logger), Is.InstanceOf<InlineSqlProvider>());
        }

        [Test]
        public void Instantiate_CommandText_InlineSqlProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            var provider = factory.Instantiate("query", DirectCommand.Instance);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider, Is.InstanceOf<InlineSqlProvider>());
        }

        private sealed class CommandFake : ICommandType
        {
            public static CommandFake Instance { get; } = new CommandFake();
        }

        private sealed class FakeProvider : ICommandProvider
        {
            public FakeProvider(string text, IQueryLogger queryLogger)
            { }

            public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => throw new NotImplementedException();
            public string Read(IDialect dialect, IConnectivity connectivity) => throw new NotImplementedException();
        }

        [Test]
        public void Add_Default_FakeProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger, CommandFake.Instance);
            factory.Add<FakeProvider>(CommandFake.Instance);

            var provider = factory.Instantiate("query");
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider, Is.InstanceOf<FakeProvider>());
        }

        [Test]
        public void Add_CommandFake_FakeProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            factory.Add<FakeProvider>(CommandFake.Instance);

            var provider = factory.Instantiate("query", CommandFake.Instance);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider, Is.InstanceOf<FakeProvider>());
        }

        [Test]
        public void Remove_CommandFake_FakeProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            factory.Add<FakeProvider>(CommandFake.Instance);
            factory.Remove(DirectCommand.Instance);

            Assert.Throws<ArgumentOutOfRangeException>(() => factory.Instantiate("query", DirectCommand.Instance));
            Assert.DoesNotThrow(() => factory.Instantiate("query", CommandFake.Instance));
        }

        [Test]
        public void Clear_CommandFake_FakeProvider()
        {
            var logger = Mock.Of<IQueryLogger>();
            var factory = new CommandProviderFactory(logger);
            factory.Add<FakeProvider>(CommandFake.Instance);
            factory.Clear();

            Assert.Throws<ArgumentOutOfRangeException>(() => factory.Instantiate("query", DirectCommand.Instance));
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.Instantiate("query", CommandFake.Instance));
        }
    }
}
