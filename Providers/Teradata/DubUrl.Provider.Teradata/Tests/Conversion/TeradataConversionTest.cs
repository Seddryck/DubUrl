using DubUrl.Provider.Teradata.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Teradata.Tests.Conversion;

public class TeradataConversionTest
{
    [TestCase(@"Data Source=localhost;Port Number=1025;Database=db;User Id=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("teradata", input, global::Teradata.Client.Provider.TdFactory.Instance,
            csb => new TeradataMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
