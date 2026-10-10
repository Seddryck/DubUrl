using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;

namespace DubUrl.Providers.MsAccess.QA;

[TestFixture, Category("MsAccess"), NonParallelizable]
public sealed class MsAccessOleDbTests : OleDbContract
{
    private SchemeRegistry? registry;

    [OneTimeSetUp]
    public void RegisterProviderFactories()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        registry = new SchemeRegistryBuilder().WithAssemblies(assemblies).WithAutoDiscoveredMappings().Build();
    }

    protected override ConnectionUrl CreateConnectionUrl() => new("oledb+accdb:///MsAccess/DubUrl.accdb", registry!);
    protected override string SelectFirstCustomerSql => "select [FullName] from [Customer] where [CustomerId]=1";
    protected override string SelectCustomerByIdSql => "select [FullName] from [Customer] where [CustomerId]=?";
}
