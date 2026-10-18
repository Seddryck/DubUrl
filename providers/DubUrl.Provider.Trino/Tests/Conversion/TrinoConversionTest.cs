using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Trino;

public class TrinoConversionTest
{
    [TestCase(@"Host=localhost;Port=8080;Catalog=example;Schema=public;User=example;Password=example;TrinoHeaders=1")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("trino", input, global::NReco.PrestoAdo.PrestoDbFactory.Instance,
            csb => new TrinoMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
