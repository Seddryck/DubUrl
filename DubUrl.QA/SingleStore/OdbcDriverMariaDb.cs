using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;

namespace DubUrl.QA.SingleStore;

[Category("SingleStore")]
[Category("MariaDBDriver")]
public class OdbcDriverMariaDb : BaseOdbcDriver
{
    public override string ConnectionString
    {
        get => $"odbc+singlestore://root:Password12!@localhost/DubUrl";
    }

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select FullName from Customer where CustomerId=1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select FullName from Customer where CustomerId=?");
}
