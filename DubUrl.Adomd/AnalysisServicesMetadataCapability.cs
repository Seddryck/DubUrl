using System.Data;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DubUrl.Adomd.Wrappers;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd;

/// <summary>Reads metadata from an Analysis Services endpoint through ADOMD.</summary>
public sealed class AnalysisServicesMetadataCapability
{
    private static readonly XNamespace Engine = "http://schemas.microsoft.com/analysisservices/2003/engine";
    private static readonly XNamespace Engine300 = "http://schemas.microsoft.com/analysisservices/2011/engine/300";

    /// <summary>
    /// Discovers the server mode using an open connection. Returns <see cref="AnalysisServicesServerMode.Unknown"/>
    /// when the XMLA response is missing, malformed, or does not identify a recognized mode.
    /// ADOMD errors from the Discover request propagate to the caller.
    /// </summary>
    /// <param name="connection">An open DubUrl ADOMD connection or native ADOMD connection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connection"/> is null.</exception>
    /// <exception cref="NotSupportedException"><paramref name="connection"/> does not use ADOMD.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="connection"/> is closed.</exception>
    public AnalysisServicesServerMode GetServerMode(IDbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var adomd = connection switch
        {
            AdomdConnectionWrapper { Connection: AdomdConnection inner } => inner,
            AdomdConnection native => native,
            _ => throw new NotSupportedException("GetServerMode requires an ADOMD connection.")
        };
        if (adomd.State != ConnectionState.Open)
            throw new InvalidOperationException("GetServerMode requires an open ADOMD connection.");

        var restrictions = new AdomdRestrictionCollection
        {
            new AdomdRestriction("ObjectExpansion", "ReferenceOnly")
        };
        using var metadata = adomd.GetSchemaDataSet("DISCOVER_XML_METADATA", restrictions);
        return ReadServerMode(metadata);
    }

    internal static AnalysisServicesServerMode ReadServerMode(DataSet metadata)
    {
        if (metadata.Tables.Count == 0)
            return AnalysisServicesServerMode.Unknown;

        var table = metadata.Tables[0];
        if (table.Rows.Count == 0 || table.Columns.Count == 0
            || table.Rows[0][0] is not string xml || string.IsNullOrWhiteSpace(xml))
            return AnalysisServicesServerMode.Unknown;

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return AnalysisServicesServerMode.Unknown;
        }

        var mode = document.Descendants(Engine300 + "ServerMode").FirstOrDefault();
        return mode?.Value.Trim() switch
        {
            "Tabular" or "SharePoint" => AnalysisServicesServerMode.Tabular,
            "Multidimensional" or "Default" => AnalysisServicesServerMode.Multidimensional,
            null when IsLegacyVersion(document) => AnalysisServicesServerMode.Multidimensional,
            _ => AnalysisServicesServerMode.Unknown
        };
    }

    private static bool IsLegacyVersion(XDocument document)
    {
        var version = document.Descendants(Engine + "Version").FirstOrDefault()?.Value.Trim();
        var major = version?.Split('.', 2)[0];
        return int.TryParse(major, NumberStyles.None, CultureInfo.InvariantCulture, out var release)
            && release is > 0 and < 11;
    }
}
