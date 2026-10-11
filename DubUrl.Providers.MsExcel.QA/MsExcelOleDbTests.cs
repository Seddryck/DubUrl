using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
namespace DubUrl.Providers.MsExcel.QA;
[TestFixture, Category("MsExcel"), NonParallelizable]
public sealed class MsExcelOleDbTests : OleDbContract
{
    private SchemeRegistry? registry;
    [OneTimeSetUp]
    public void RegisterProviderFactories()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        registry = new SchemeRegistryBuilder().WithAssemblies(assemblies).WithAutoDiscoveredMappings().Build();
    }
    protected override ConnectionUrl CreateConnectionUrl() => new("oledb+xlsx:///MsExcel/Customer.xlsx", registry!);
    protected override string SelectFirstCustomerSql => "select [FullName] from [Customer$] where [CustomerId]=1";
    protected override string SelectCustomerByIdSql => "select [FullName] from [Customer$] where [CustomerId]=?";
}
