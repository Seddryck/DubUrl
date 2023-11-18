using System.Collections.Generic;
using DubUrl.Mapping;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using DubUrl.Querying;
using DubUrl.MicroOrm;
using DubUrl.Extensions.DependencyInjection;

namespace DubUrl.Extensions.Testing.DependencyInjection
{
    [TestFixture]
    public class DubUrlServiceExtensionsTest
    {
        [Test]
        public void AddDubUrlTest_Default()
        {
            using var provider = new ServiceCollection()
                .AddDubUrl()
                .AddSingleton(Configuration)
                .BuildServiceProvider();
            SchemeMapperBuilder? mapper = null;
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() => mapper = provider.GetRequiredService<SchemeMapperBuilder>());
                Assert.That(mapper, Is.Not.Null);
            });
        }

        [Test]
        public void AddDubUrlTest_Default_SupportsLogging()
        {

            using var provider = new ServiceCollection()
                .AddDubUrl()
                .AddSingleton(Configuration)
                .AddLogging()
                .BuildServiceProvider();
            Assert.DoesNotThrow(() => provider.GetRequiredService<SchemeMapperBuilder>());
            var options = provider.GetRequiredService<IOptions<DubUrlServiceOptions>>();
            Assert.That(options.Value.Logger, Is.Not.Null);
        }

        [Test]
        public void AddDubUrlTest_WithOptions_EmptyConfiguration()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyConfiguration)
                .AddDubUrl(options)
                .BuildServiceProvider();
            SchemeMapperBuilder? mapper = null;
            Assert.DoesNotThrow(() => mapper = provider.GetRequiredService<SchemeMapperBuilder>());
        }

        [Test]
        public void AddDubUrlTest_WithoutQueryLogger_ReturnsNullQuerLoggerInDatabaseUrlFactory()
        {
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyConfiguration)
                .AddDubUrl(options)
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
            Assert.That(factory.QueryLogger, Is.EqualTo(NullQueryLogger.Instance));
        }

        [Test]
        public void WithQueryLogger_StubQueryLogger_ReturnsStubQuerLoggerInDatabaseUrlFactory()
        {
            var logger = Mock.Of<IQueryLogger>();
            var options = new DubUrlServiceOptions();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyConfiguration)
                .AddDubUrl(options)
                .WithQueryLogger(logger)
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
            Assert.That(factory.QueryLogger, Is.EqualTo(logger));
            Assert.That(factory.QueryLogger, Is.Not.EqualTo(NullQueryLogger.Instance));
        }

        [Test]
        public void WithoutCache_None_ReturnsWithoutCacheForOrmDatabaseUrl()
        {
            var logger = Mock.Of<IQueryLogger>();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyConfiguration)
                .AddDubUrl()
                .WithMicroOrm()
                .WithQueryLogger(logger)
                .WithoutReflectionCache()
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
            Assert.That(factory, Is.TypeOf<MicroOrm.DatabaseUrlFactory>());
            Assert.That(factory.QueryLogger, Is.EqualTo(logger));
            var microOrm = (factory as MicroOrm.DatabaseUrlFactory)!;
            Assert.That(microOrm.ReflectionCache, Is.TypeOf<NoneReflectionCache>());
        }

        [Test]
        public void WithCache_Stub_ReturnsStubCacheForOrmDatabaseUrl()
        {
            var logger = Mock.Of<IQueryLogger>();
            var cache = Mock.Of<IReflectionCache>();
            using var provider = new ServiceCollection()
                .AddSingleton(EmptyConfiguration)
                .AddDubUrl()
                .WithMicroOrm()
                .WithQueryLogger(logger)
                .WithReflectionCache(cache)
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
            Assert.That(factory, Is.TypeOf<MicroOrm.DatabaseUrlFactory>());
            Assert.That(factory.QueryLogger, Is.EqualTo(logger));
            var microOrm = (factory as MicroOrm.DatabaseUrlFactory)!;
            Assert.That(microOrm.ReflectionCache, Is.EqualTo(cache));
        }

        private static IConfiguration Configuration
        {
            get
            {
                var configuration = new Dictionary<string, string?>
                { };
                var builder = new ConfigurationBuilder().AddInMemoryCollection(configuration);
                return builder.Build();
            }
        }

        private static IConfiguration EmptyConfiguration
        {
            get
            {
                var builder = new ConfigurationBuilder().AddInMemoryCollection();
                return builder.Build();
            }
        }
    }
}
