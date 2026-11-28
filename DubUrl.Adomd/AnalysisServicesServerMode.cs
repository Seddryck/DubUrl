namespace DubUrl.Adomd;

/// <summary>Server modes used to select Analysis Services metadata discovery.</summary>
public enum AnalysisServicesServerMode
{
    /// <summary>The server mode could not be determined.</summary>
    Unknown,
    /// <summary>Multidimensional mode, including the legacy Default mode.</summary>
    Multidimensional,
    /// <summary>Tabular mode, including SharePoint integrated mode.</summary>
    Tabular
}
