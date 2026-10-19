using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Odbc.Tests.Conversion;

public class OdbcConversionTest
{
    [TestCase(@"Driver={Example driver};Server=localhost;Database=db;Uid=example;Pwd=example;Extra={a;b}")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("odbc+mssql", input, global::System.Data.Odbc.OdbcFactory.Instance,
            csb => new OdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
