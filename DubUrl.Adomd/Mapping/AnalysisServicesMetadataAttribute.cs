using DubUrl.Mapping;

namespace DubUrl.Adomd.Mapping;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class AnalysisServicesMetadataAttribute : ProviderCapabilityAttribute
{
    public override object CreateCapability() => new AnalysisServicesMetadataCapability();
}
