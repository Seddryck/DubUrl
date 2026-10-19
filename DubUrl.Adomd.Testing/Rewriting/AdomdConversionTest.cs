using DubUrl.Adomd.Mapping;
using DubUrl.Adomd.Wrappers;
using DubUrl.Mapping;
using DubUrl.Testing.Conversion;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Adomd.Testing.Rewriting;

public class AdomdConversionTest
{
    [TestCase("ssas", @"Data Source=localhost\instance;Database=db;Cube=cube;User ID=example;Password=example;Integrated Security=sspi")]
    [TestCase("ssas", "Data Source=localhost:1234;Database=db;Integrated Security=sspi")]
    [TestCase("tabular", "Data Source=localhost;Database=db;Integrated Security=sspi")]
    [TestCase("desktop", "Data Source=localhost:1234")]
    [TestCase("premium", "Data Source=powerbi://api.powerbi.com/v1.0/myorg/example")]
    [TestCase("azure-explicit", "Data Source=asazure://westus.asazure.windows.net/example;Cube=db")]
    public void Convert_PreservesProviderSettings(string scheme, string input)
    {
        IMapper Create(DbConnectionStringBuilder csb) => scheme switch
        {
            "ssas" => new SsasMultidimMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer),
            "tabular" => new SsasTabularMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer),
            "desktop" => new PowerBiDesktopMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer),
            "premium" => new PowerBiPremiumMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer),
            _ => new AzureDescriptorMapper(csb),
        };
        var uri = ConversionTestSupport.RoundTrip(scheme, input, AdomdFactory.Instance, Create);
        Assert.That(uri.Scheme.Canonical, Is.EqualTo(scheme));
    }

    [Test]
    public void Convert_AzureDescriptor_RejectsIncompatibleBuiltInForwardMapper()
    {
        // AsAzureMapper currently constructs SsasTabularRewriter; do not change forward behavior here.
        var registry = ConversionTestSupport.Registry("asazure", AdomdFactory.Instance,
            csb => new AsAzureMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
        var converter = new DubUrl.Conversion.ConnectionStringConverter(registry);
        Assert.That(converter.TryConvert("asazure",
            "Data Source=asazure://westus.asazure.windows.net/example;Cube=db", out _, out var diagnostic), Is.False);
        Assert.That(diagnostic!.Code, Is.EqualTo(DubUrl.Conversion.ConnectionStringConversionDiagnosticCode.SettingsNotRepresentable));
    }

    private sealed class AzureDescriptorMapper(DbConnectionStringBuilder csb) : BaseMapper(
        new DubUrl.Adomd.Rewriting.AsAzureRewriter(csb),
        ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer);
}
