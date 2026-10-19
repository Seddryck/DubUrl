using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class TrinoOdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Host=localhost;Port=8080;Catalog=example;Schema=public;Uid=example;Pwd=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+trino", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new TrinoOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
