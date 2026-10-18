using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Teradata;

public class TeradataConversionTest
{
    [TestCase(@"Data Source=localhost;Port Number=1025;Database=db;User Id=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("teradata", input, global::Teradata.Client.Provider.TdFactory.Instance,
            csb => new TeradataMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
