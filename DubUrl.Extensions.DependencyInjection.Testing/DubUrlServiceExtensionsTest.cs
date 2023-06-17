using System.Collections.Generic;
using DubUrl.Mapping;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace DubUrl.Extensions.DependencyInjection.Testing
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

        private static IConfiguration Configuration
        {
            get
            {
                var configuration = new Dictionary<string, string?>
                {
                    
                };
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