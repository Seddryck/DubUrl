using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.SqlServer;

public class MsSqlServerConversionTest
{
    [TestCase(@"Data Source=localhost;Initial Catalog=db;Integrated Security=True")]
    [TestCase(@"Server=localhost\instance,1435;Database=db;UID=example;PWD=""p;a@ss+%&"";Encrypt=True")]
    [TestCase(@"Server=localhost;Database=""db /é"";Integrated Security=False")]
    [TestCase(@"Server=[::1],1433;Database=db;User ID=example;Password=example")]
    [TestCase(@"Server=localhost;Database=db;Application Name=;Integrated Security=True")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("mssql", input, global::Microsoft.Data.SqlClient.SqlClientFactory.Instance,
            csb => new MsSqlServerMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
