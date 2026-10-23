using DubUrl.Provider.Db2.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Db2.Tests.Conversion;

public class Db2ConversionTest
{
    [TestCase(@"Server=localhost:50000;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("db2", input, global::IBM.Data.Db2.DB2Factory.Instance,
            csb => new Db2Mapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
