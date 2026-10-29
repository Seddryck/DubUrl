using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using NUnit.Framework;
namespace DubUrl.Provider.MsExcel.QA;
[TestFixture, Category("MsExcel"), NonParallelizable]
public sealed class MsExcelOleDbTests : OleDbContract
{
    private SchemeRegistry? registry;
    protected override ISchemeRegistry CliRegistry => registry!;
    [OneTimeSetUp]
    public void RegisterProviderFactories()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, .. assemblies])).Build();
    }
    protected override ConnectionUrl CreateConnectionUrl() => new("oledb+xlsx:///MsExcel/Customer.xlsx", registry!);
    protected override string SelectFirstCustomerSql => "select [FullName] from [Customer$] where [CustomerId]=1";
    protected override string SelectCustomerByIdSql => "select [FullName] from [Customer$] where [CustomerId]=?";
}
