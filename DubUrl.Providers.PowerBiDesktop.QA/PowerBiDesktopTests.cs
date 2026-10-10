using DubUrl.Adomd.Mapping;
using DubUrl.Mapping;
using DubUrl.ProviderTesting;
using DubUrl.Registering;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
namespace DubUrl.Providers.PowerBiDesktop.QA;
[TestFixture, Category("PowerBiDesktop"), NonParallelizable, Platform("Win")]
public sealed class PowerBiDesktopTests : AdomdContract
{
    protected override SchemeRegistry CreateSchemeRegistry()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(PowerBiDesktopDatabase).Assembly };
        new ProviderFactoriesRegistrator(new BinFolderDiscoverer(assemblies)).Register();
        return new SchemeRegistryBuilder().WithAssemblies(assemblies).WithAutoDiscoveredMappings().Build();
    }
    protected override string ConnectionUrl => "pbix://localhost/Customer";
    protected override string InvalidConnectionUrl => "pbix://localhost/Missing";
    protected override string SelectFirstCustomerSql => "EVALUATE SELECTCOLUMNS(FILTER(Customer, Customer[CustomerId] = 1), \"FullName\", Customer[FullName])";
    protected override string SelectFirstCustomerRowSql => "EVALUATE SELECTCOLUMNS(FILTER(Customer, Customer[CustomerId] = 1), \"CustomerId\", Customer[CustomerId], \"FullName\", Customer[FullName], \"BirthDate\", Customer[BirthDate])";
    protected override string SelectCustomerByIdSql => "EVALUATE SELECTCOLUMNS(FILTER(Customer, Customer[CustomerId] = @CustId), \"FullName\", Customer[FullName])";
    protected override string SelectAllCustomersSql => "EVALUATE SELECTCOLUMNS(Customer, \"CustomerId\", Customer[CustomerId], \"FullName\", Customer[FullName], \"BirthDate\", Customer[BirthDate])";
    protected override string SelectYoungestCustomersSql => "EVALUATE SELECTCOLUMNS(TOPN(2, Customer, Customer[BirthDate], DESC), \"CustomerId\", Customer[CustomerId], \"FullName\", Customer[FullName], \"BirthDate\", Customer[BirthDate])";
    protected override string SelectWhereCustomersTemplate => "EVALUATE SELECTCOLUMNS(FILTER($table; format=\"identity\"$, $clauses:{clause | $table; format=\"identity\"$[$clause.Field$] $clause.Operator$ $clause.Value; format=\"value\"$}; separator=\" && \"$), $fields:{field | $field; format=\"value\"$, $table; format=\"identity\"$[$field$]}; separator=\", \"$)";
    protected override bool DbReaderUsesDapper => true;
}
