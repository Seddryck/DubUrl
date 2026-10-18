using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.MySql;

public class MariaDbDataConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mariadb", input, global::MySql.Data.MySqlClient.MySqlClientFactory.Instance,
            csb => new DubUrl.Mapping.Implementation.MariaDbDataRewriter(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
