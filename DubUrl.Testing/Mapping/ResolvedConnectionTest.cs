using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing.Mapping;

public class ResolvedConnectionTest
{
    [Test]
    public void ToString_ConnectionString_RedactedByDefault()
    {
        const string connectionString = "Server=host;Password=secret";
        var resolved = new ResolvedConnection(
            connectionString,
            Mock.Of<IDialect>(),
            Mock.Of<IConnectivity>(),
            Mock.Of<IParametrizer>(),
            Mock.Of<DbProviderFactory>());

        Assert.Multiple(() =>
        {
            Assert.That(resolved.ToString(), Does.Not.Contain(connectionString));
            Assert.That(resolved.ToString(), Does.Not.Contain("secret"));
            Assert.That(resolved.ToString(redactConnectionString: false), Does.Contain(connectionString));
        });
    }
}
