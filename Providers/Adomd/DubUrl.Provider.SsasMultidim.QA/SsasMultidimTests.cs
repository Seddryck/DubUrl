using DubUrl.Provider.Odbc.Rewriting;
using DubUrl.Adomd.Mapping;
using DubUrl.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using NUnit.Framework;

namespace DubUrl.Provider.SsasMultidim.QA;

[TestFixture, Category("SsasMultidim"), NonParallelizable, Platform("Win")]
[Ignore("A pre-provisioned SSAS Multidimensional AdventureWorks instance is required; the legacy QA harness did not deploy one.")]
public sealed class SsasMultidimTests : AdomdContract
{
    protected override SchemeRegistry CreateSchemeRegistry()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(SsasMultidimDatabase).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        return new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, .. assemblies])).Build();
    }

    protected override string ConnectionUrl => "ssasmultidim://localhost/multidim/AdventureWorks";
    protected override string InvalidConnectionUrl => "ssasmultidim://localhost/multidim/Missing";
    protected override string SelectFirstCustomerSql => string.Empty;
    protected override string SelectFirstCustomerRowSql => string.Empty;
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override string SelectAllCustomersSql => string.Empty;
    protected override string SelectYoungestCustomersSql => string.Empty;
    protected override string SelectWhereCustomersTemplate => string.Empty;
}
