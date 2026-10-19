using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.DuckDb;

public class DuckdbConversionTest
{
    [TestCase(@"DataSource=:memory:")]
    [TestCase(@"DataSource=example.duckdb")]
    [TestCase(@"DataSource=""C:\data\example db.duckdb""")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("duckdb", input, global::DuckDB.NET.Data.DuckDBClientFactory.Instance,
            csb => new DuckdbMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
