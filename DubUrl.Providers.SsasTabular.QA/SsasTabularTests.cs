using DubUrl.Adomd.Mapping;
using DubUrl.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;

namespace DubUrl.Providers.SsasTabular.QA;

[TestFixture, Category("SsasTabular"), NonParallelizable, Platform("Win")]
[Ignore("A pre-provisioned SSAS Tabular AdventureWorks instance is required; the legacy QA harness did not deploy one.")]
public sealed class SsasTabularTests : AdomdContract
{
    protected override SchemeRegistry CreateSchemeRegistry()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(SsasTabularDatabase).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        return new SchemeRegistryBuilder().WithAssemblies(assemblies).WithAutoDiscoveredMappings().Build();
    }

    protected override string ConnectionUrl => "ssastabular://localhost/tabular/AdventureWorks";
    protected override string InvalidConnectionUrl => "ssastabular://localhost/tabular/Missing";
    protected override string SelectFirstCustomerSql => string.Empty;
    protected override string SelectFirstCustomerRowSql => string.Empty;
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override string SelectAllCustomersSql => string.Empty;
    protected override string SelectYoungestCustomersSql => string.Empty;
    protected override string SelectWhereCustomersTemplate => string.Empty;
}
