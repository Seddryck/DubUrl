using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class DuckdbOdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Server=localhost;Database=example.duckdb")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+duckdb", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new DuckdbOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
