using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.Tests.Conversion;

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
