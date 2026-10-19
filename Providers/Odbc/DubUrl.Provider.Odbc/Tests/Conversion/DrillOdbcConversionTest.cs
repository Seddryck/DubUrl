using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class DrillOdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Server=localhost;Port=31010;Schema=example;AuthenticationType=No Authentication;ConnectionType=Direct")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+drill", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new DrillOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
