using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using DubUrl.Querying.Reading;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using NUnit.Framework;
using DubUrl.Mapping;

namespace DubUrl.Testing.MicroOrm;

public class DatabaseUrlFactoryTest
{
    [Test]
    public void Instantiate_Url_AnyDatabaseUrl()
    {
        var connectionUrlFactory = new Mock<ConnectionUrlFactory>(new[] { Mock.Of<SchemeMapperBuilder>() });
        connectionUrlFactory.Setup(x => x.Instantiate(It.IsAny<string>()))
            .Returns(new Mock<ConnectionUrl>(new[] { "any://host" }).Object);

        var factory = new DubUrl.MicroOrm.DatabaseUrlFactory(
                connectionUrlFactory.Object
                , Mock.Of<CommandProvisionerFactory>()
                , Mock.Of<IReflectionCache>()
                , Mock.Of<IQueryLogger>()
            );

        Assert.That(factory.Instantiate("any://host"), Is.Not.Null);
        connectionUrlFactory.Verify(x => x.Instantiate("any://host"), Times.Once);
    }
}
