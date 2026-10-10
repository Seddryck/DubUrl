using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.MsAccess.QA;

[TestFixture, Category("MsAccess"), NonParallelizable]
public sealed class MsAccessOdbcTests : OdbcContract
{
    private static string CurrentDirectory => Path.GetDirectoryName(typeof(MsAccessOdbcTests).Assembly.Location) + "\\";
    protected override string ConnectionUrl => $"odbc+accdb:///MsAccess/DubUrl.accdb?Defaultdir={CurrentDirectory}";
    protected override string SelectFirstCustomerSql => "select [FullName] from [Customer] where [CustomerId]=1";
    protected override string SelectCustomerByIdSql => "select [FullName] from [Customer] where [CustomerId]=?";
}
