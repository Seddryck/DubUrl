using System.Data;
using System.Data.Common;
using DubUrl.Adomd.Mapping;
using DubUrl.Adomd.Wrappers;
using DubUrl.Mapping;
using Moq;
using NUnit.Framework;

namespace DubUrl.Adomd.Testing;

public class AnalysisServicesMetadataCapabilityTest
{
    [TestCase("ssastabular")]
    [TestCase("ssasmdx")]
    [TestCase("asa")]
    [TestCase("powerbi")]
    [TestCase("pbix")]
    public void TryGetCapability_AdomdScheme_ExposesMetadata(string scheme)
    {
        DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient", AdomdFactory.Instance);
        var registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule(typeof(SsasTabularMapper).Assembly))
            .Build();
        var url = new ConnectionUrl($"{scheme}://localhost/catalog", registry);

        Assert.That(url.TryGetCapability<AnalysisServicesMetadataCapability>(out var metadata), Is.True);
        Assert.That(metadata, Is.Not.Null);
    }

    [Test]
    public void GetServerMode_NonAdomdConnection_ThrowsNotSupported()
    {
        var metadata = new AnalysisServicesMetadataCapability();
        using var connection = Mock.Of<IDbConnection>();

        Assert.That(() => metadata.GetServerMode(connection), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void GetServerMode_ClosedAdomdConnection_ThrowsInvalidOperation()
    {
        var metadata = new AnalysisServicesMetadataCapability();
        using var connection = AdomdFactory.Instance.CreateConnection();

        Assert.That(() => metadata.GetServerMode(connection), Throws.TypeOf<InvalidOperationException>());
    }

    [TestCase("Tabular", "16.0.1", AnalysisServicesServerMode.Tabular)]
    [TestCase("Multidimensional", "16.0.1", AnalysisServicesServerMode.Multidimensional)]
    [TestCase("SharePoint", null, AnalysisServicesServerMode.Tabular)]
    [TestCase("Default", null, AnalysisServicesServerMode.Multidimensional)]
    [TestCase(null, "10.50.6000.34", AnalysisServicesServerMode.Multidimensional)]
    [TestCase(null, "11.0.1", AnalysisServicesServerMode.Unknown)]
    [TestCase(null, "invalid", AnalysisServicesServerMode.Unknown)]
    [TestCase(null, null, AnalysisServicesServerMode.Unknown)]
    [TestCase("", "10.50.6000.34", AnalysisServicesServerMode.Unknown)]
    [TestCase("FutureMode", "10.50.6000.34", AnalysisServicesServerMode.Unknown)]
    public void ReadServerMode_ServerMetadata_ReturnsExpectedMode(
        string? mode, string? version, AnalysisServicesServerMode expected)
    {
        var modeElement = mode is null ? "" : $"<ddl300:ServerMode>{mode}</ddl300:ServerMode>";
        var versionElement = version is null ? "" : $"<Version>{version}</Version>";
        var xml = $"""
            <Server xmlns="http://schemas.microsoft.com/analysisservices/2003/engine"
                    xmlns:ddl300="http://schemas.microsoft.com/analysisservices/2011/engine/300">
              <Name>Example</Name>
              {versionElement}
              {modeElement}
            </Server>
            """;
        using var metadata = new DataSet();
        var table = metadata.Tables.Add();
        table.Columns.Add("METADATA", typeof(string));
        table.Rows.Add(xml);

        Assert.That(AnalysisServicesMetadataCapability.ReadServerMode(metadata), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase(" ")]
    [TestCase("<Server>")]
    public void ReadServerMode_UnavailableXml_ReturnsUnknown(string? xml)
    {
        using var metadata = new DataSet();
        var table = metadata.Tables.Add();
        table.Columns.Add("METADATA", typeof(string));
        table.Rows.Add((object?)xml ?? DBNull.Value);

        Assert.That(AnalysisServicesMetadataCapability.ReadServerMode(metadata), Is.EqualTo(AnalysisServicesServerMode.Unknown));
    }

    [Test]
    public void ReadServerMode_EmptyRowset_ReturnsUnknown()
    {
        using var metadata = new DataSet();
        var table = metadata.Tables.Add();
        table.Columns.Add("METADATA", typeof(string));

        Assert.That(AnalysisServicesMetadataCapability.ReadServerMode(metadata), Is.EqualTo(AnalysisServicesServerMode.Unknown));
    }
}
