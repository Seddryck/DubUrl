using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Sqlite;

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
