using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.MySql.Rewriting;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Conversion;

public class MariaDbDataConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mariadb", input, global::MySql.Data.MySqlClient.MySqlClientFactory.Instance,
            csb => new DubUrl.Provider.MySql.Mapping.MariaDbDataRewriter(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
