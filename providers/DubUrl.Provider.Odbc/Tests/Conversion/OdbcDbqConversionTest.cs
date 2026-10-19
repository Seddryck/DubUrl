using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Odbc;

public class OdbcDbqConversionTest
{
    [TestCase(@"Driver={Example driver};DBQ=C:\data\example.csv;Uid=example;Pwd=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+text", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new OdbcDbqMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
