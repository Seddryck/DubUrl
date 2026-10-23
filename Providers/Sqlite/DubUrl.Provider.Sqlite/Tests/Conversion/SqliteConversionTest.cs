using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.Tests.Conversion;

public class SqliteConversionTest
{
    [TestCase(@"Data Source=:memory:")]
    [TestCase(@"Data Source=sample.db;Mode=ReadOnly")]
    [TestCase(@"Data Source=""C:\data\example db.sqlite"";Foreign Keys=True")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("sqlite", input, global::Microsoft.Data.Sqlite.SqliteFactory.Instance,
            csb => new SqliteMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
