using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Db2;

public class Db2ConversionTest
{
    [TestCase(@"Server=localhost:50000;Database=db;User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("db2", input, global::IBM.Data.Db2.DB2Factory.Instance,
            csb => new Db2Mapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
