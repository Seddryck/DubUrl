using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.Text.QA;

[TestFixture, Category("Text"), NonParallelizable]
public sealed class TextOdbcTests : OdbcContract
{
    private static string CurrentDirectory => Path.GetDirectoryName(typeof(TextOdbcTests).Assembly.Location) + "\\";
    protected override string ConnectionUrl => $"odbc+csv:///Text?Defaultdir={CurrentDirectory}";
    protected override string SelectFirstCustomerSql => "select FullName from Customer.csv where CustomerId=1";
    protected override string SelectCustomerByIdSql => "select FullName from Customer.csv where CustomerId=?";
}
