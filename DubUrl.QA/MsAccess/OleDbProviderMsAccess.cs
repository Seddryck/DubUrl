using NUnit.Framework;

namespace DubUrl.QA.MsAccess;

[Category("MsAccess")]
[Category("OleDB")]
public class OleDbProviderMsAccess : BaseOleDbProvider
{
    public override string ConnectionString
        => $"oledb+accdb:///MsAccess/DubUrl.accdb";

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select[FullName] from [Customer] where [CustomerId]=1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select [FullName] from [Customer] where [CustomerId]=?");
}
