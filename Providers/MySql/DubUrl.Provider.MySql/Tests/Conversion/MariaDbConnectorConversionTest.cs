using DubUrl.Provider.MySql.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Conversion;

public class MariaDbConnectorConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mariadb", input, global::MySqlConnector.MySqlConnectorFactory.Instance,
            csb => new MariaDbConnectorMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
