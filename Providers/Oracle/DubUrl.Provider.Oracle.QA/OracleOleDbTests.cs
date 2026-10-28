using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.ProviderTesting;
using NUnit.Framework;
using System.Data.Common;
using System.Data.OleDb;

namespace DubUrl.Provider.Oracle.QA;

[TestFixture, Category("Oracle"), Platform("Win")]
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public sealed class OracleOleDbTests : OleDbContract
{
    protected override ConnectionUrl CreateConnectionUrl()
    {
        DbProviderFactories.RegisterFactory("System.Data.OleDb", OleDbFactory.Instance);
        var registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(
            [.. new AssemblyTypesProbe().Assemblies, typeof(OleDbRewriter).Assembly, typeof(Provider.Oracle.Sql.OracleDialect).Assembly])).Build();
        var url = Environment.GetEnvironmentVariable("DUBURL_ORACLE_OLEDB_QA_URL")
            ?? throw new InvalidOperationException("Run the Windows Oracle OLE DB deployment script first.");
        return new ConnectionUrl(url, registry);
    }
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
    [Test]
    public void ReadsOracleDateNumberAndNull()
    {
        using var connection = CreateConnectionUrl().Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select cast(17.505 as number(12,3)),date '2023-06-10',cast(null as varchar2(10)) from dual";
        using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetDecimal(0), Is.EqualTo(17.505m));
        Assert.That(reader.GetDateTime(1), Is.EqualTo(new DateTime(2023,6,10)));
        Assert.That(reader.IsDBNull(2), Is.True);
    }
}
