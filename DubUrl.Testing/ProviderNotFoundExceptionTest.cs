using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Testing
{
    public class ProviderNotFoundExceptionTest
    {

        [Test]
        public void ctor_Any_MentionsProviderSearched()
        {
            var ex = new ProviderNotFoundException("Microsoft.Data.SqlClient", Array.Empty<string>());
            Assert.That(ex.Message, Does.Contain("The ADO.Net provider corresponding to the invariant name 'Microsoft.Data.SqlClient' is not registered."));
        }

        [Test]
        public void ctor_NoProviderListed_MentionsEmpty()
        {
            var ex = new ProviderNotFoundException("Microsoft.Data.SqlClient", Array.Empty<string>());
            Assert.That(ex.Message, Does.Contain(" empty"));
        }

        [Test]
        public void ctor_SomeProviderListed_MentionsTheListOfProviders()
        {
            var ex = new ProviderNotFoundException("Microsoft.Data.SqlClient", new[] { "Npgsql", "MysqlConnector" });
            Assert.That(ex.Message, Does.Contain("'Npgsql', 'MysqlConnector'"));
            Assert.That(ex.Message, Does.Not.Contain("empty"));
        }
    }
}
