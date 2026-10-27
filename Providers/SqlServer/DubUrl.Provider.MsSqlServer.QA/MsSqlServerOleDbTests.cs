using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using NUnit.Framework;
namespace DubUrl.Provider.MsSqlServer.QA;
[TestFixture, Category("MsSqlServer"), Platform("Win")]
public sealed class MsSqlServerOleDbTests : OleDbContract
{
    private SchemeRegistry? registry;
    [OneTimeSetUp]
    public void RegisterProviderFactories()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, .. assemblies])).Build();
    }
    protected override ConnectionUrl CreateConnectionUrl() => new(
        Environment.GetEnvironmentVariable("DUBURL_SQLSERVER_OLEDB_QA_URL")
            ?? throw new InvalidOperationException("Run the Windows SQL Server OLE DB deployment script first."), registry!);
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer where CustomerId=?";
}
