using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class QuestDbOdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Server=localhost;Port=8812;Uid=example;Pwd=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+questdb", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new QuestDbOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
