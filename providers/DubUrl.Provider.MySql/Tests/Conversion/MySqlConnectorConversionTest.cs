using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.MySql;

public class MySqlConnectorConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=""p;a@ss+%&"";Pooling=False")]
    [TestCase(@"Server=::1;Port=3307;Database=db;User ID=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mysql", input, global::MySqlConnector.MySqlConnectorFactory.Instance,
            csb => new MySqlConnectorMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
