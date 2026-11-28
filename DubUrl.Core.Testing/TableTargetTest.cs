using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.TypeMapping;
using Moq;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Core.Testing;

public partial class TableTargetTest
{
    private static DatabaseUrl CreateDatabase(DbProviderFactory factory, IDialect? dialect = null)
    {
        dialect ??= new AnsiDialect(new SqlLanguage(), [], new AnsiRenderer(), [],
            AnsiTypeMapper.Instance, AnsiFunctionMapper.Instance);
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(new ResolvedConnection(
            "", dialect, Mock.Of<IConnectivity>(), Mock.Of<IParametrizer>(), factory));
        return new DatabaseUrl(new ConnectionUrl("test://localhost", registry.Object), new(), NullQueryLogger.Instance);
    }

    [Test]
    public void Table_QuotesEachLiteralComponentWithoutConnecting()
    {
        var factory = new Mock<DbProviderFactory>(MockBehavior.Strict);
        using var database = CreateDatabase(factory.Object);
        var target = database.Table(name: "order.items", schema: "daily\"sales");

        Assert.Multiple(() =>
        {
            Assert.That(target.Name, Is.EqualTo("order.items"));
            Assert.That(target.Schema, Is.EqualTo("daily\"sales"));
            Assert.That(target.Database, Is.Null);
            Assert.That(target.Catalog, Is.Null);
            Assert.That(target.SqlName, Is.EqualTo("\"daily\"\"sales\".\"order.items\""));
            Assert.That(database.Table(name: "sales.orders").SqlName, Is.EqualTo("\"sales.orders\""));
        });
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public void Table_RejectsEmptyNamesAndUnsupportedQualifiers()
    {
        using var database = CreateDatabase(Mock.Of<DbProviderFactory>());

        Assert.Throws<ArgumentNullException>(() => database.Table(null!));
        Assert.Throws<ArgumentException>(() => database.Table(" "));
        Assert.Throws<ArgumentException>(() => database.Table("orders", schema: ""));
        Assert.Throws<NotSupportedException>(() => database.Table("orders", database: "sales"));
        Assert.Throws<NotSupportedException>(() => database.Table("orders", catalog: "sales"));
    }
}
