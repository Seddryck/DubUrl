namespace DubUrl.Mapping;

/// <summary>Supplies an optional capability for an attributed provider mapper.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public abstract class ProviderCapabilityAttribute : Attribute
{
    public abstract object CreateCapability();
}
