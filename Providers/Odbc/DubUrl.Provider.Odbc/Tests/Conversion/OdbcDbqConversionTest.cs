using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class OdbcDbqConversionTest
{
    [TestCase(@"Driver={Example driver};DBQ=C:\data\example.csv;Uid=example;Pwd=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+text", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new OdbcDbqMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
