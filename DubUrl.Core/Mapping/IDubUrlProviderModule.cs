namespace DubUrl.Mapping;

/// <summary>
/// Registers a cohesive set of database providers with a DubUrl catalog.
/// </summary>
public interface IDubUrlProviderModule
{
    void Register(IProviderCatalogBuilder catalog);
}
