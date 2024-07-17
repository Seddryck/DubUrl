using NUnit.Framework;

namespace DubUrl.QA.SsasMultidim;

[Category("SsasMultidim")]
[Category("AdomdProvider")]
public class AdomdProviderSsasMultidim : BaseAdomdProvider
{
    public override string ConnectionString
        => $"ssasmultidim://localhost/multidim/AdventureWorks";

    [Test]
    public override void QueryCustomer()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerScalarValueWithReader()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDatabase()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithParams()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDapper()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDbReader()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryStringWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryBooleanWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryNumericWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryTimestampWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryDateWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryTimeWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    [Ignore("Can't return a constant in DAX?")]
    public override void QueryIntervalWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    [Category("DatabaseUrl")]
    [Category("Primitive")]
    public override void QueryNullWithDatabaseUrl()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithRepository()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithRepositoryFactory()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDapperRepository()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithDatabaseUrlAndQueryClass()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryCustomerWithWhereClause()
        => Assert.Ignore("Test not implemented");

    [Test]
    public override void QueryTwoYoungestCustomersWithRepositoryFactory()
        => Assert.Ignore("Test not implemented");
}
