using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;
using System.Drawing;

namespace DubUrl.QA.MsAccess;

[Category("MsAccess")]
[Category("ODBC")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public class OdbcDriverMsAccess : BaseOdbcDriver
{
    protected string CurrentDirectory
        => Path.GetDirectoryName(GetType().Assembly.Location) + "\\";

    public override string ConnectionString
        => $"odbc+accdb:///MsAccess/DubUrl.accdb?Defaultdir={CurrentDirectory}";

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select [FullName] from [Customer] where [CustomerId]=1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select [FullName] from [Customer] where [CustomerId]=?");
}
