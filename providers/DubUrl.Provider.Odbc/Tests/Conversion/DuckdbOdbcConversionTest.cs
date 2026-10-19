using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Odbc;

public class DuckdbOdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Server=localhost;Database=example.duckdb")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+duckdb", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new DuckdbOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
