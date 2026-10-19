using DubUrl.Provider.MySql.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Conversion;

public class MySqlDataConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mysql", input, global::MySql.Data.MySqlClient.MySqlClientFactory.Instance,
            csb => new MySqlDataMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
