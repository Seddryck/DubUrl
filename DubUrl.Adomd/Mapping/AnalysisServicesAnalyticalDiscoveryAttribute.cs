using DubUrl.Adomd.Discovery;
using DubUrl.Discovery;
using DubUrl.Mapping;

namespace DubUrl.Adomd.Mapping;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class AnalysisServicesAnalyticalDiscoveryAttribute(bool tabularScheme = true) : ProviderCapabilityAttribute
{
    public override object CreateCapability() => new AnalyticalDiscoveryCapability(new AnalysisServicesAnalyticalReader(tabularScheme));
}
