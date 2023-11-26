using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using System.Data.Odbc;
using System.Drawing;

namespace DubUrl.QA.MsExcel;

[Category("MsExcel")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public class OdbcDriverMsExcel : BaseOdbcDriver
{
    protected string CurrentDirectory
    {
        get => Path.GetDirectoryName(GetType().Assembly.Location) + "\\";
    }

    public override string ConnectionString
    {
        get => $"odbc+xlsx:///MsExcel/customer.xlsx?Defaultdir={CurrentDirectory}";
    }

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select [FullName] from [Customer$] where [CustomerId]=1");

    [Test]
    public override void QueryCustomerWithParams()
        => QueryCustomerWithParams("select [FullName] from [Customer$] where [CustomerId]=?");
}