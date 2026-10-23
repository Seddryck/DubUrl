using DubUrl.Provider.MySql.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Conversion;

public class SingleStoreConversionTest
{
    [TestCase(@"Server=localhost;Database=db;User ID=example;Password=example;Pooling=False")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("singlestore", input, global::SingleStoreConnector.SingleStoreConnectorFactory.Instance,
            csb => new SingleStoreMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
