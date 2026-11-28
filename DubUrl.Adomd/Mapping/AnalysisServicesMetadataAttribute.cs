using DubUrl.Mapping;

namespace DubUrl.Adomd.Mapping;

internal sealed class AnalysisServicesMetadataAttribute : ProviderCapabilityAttribute
{
    public override object CreateCapability() => new AnalysisServicesMetadataCapability();
}
