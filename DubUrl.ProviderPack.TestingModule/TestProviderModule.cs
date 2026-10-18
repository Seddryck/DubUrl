using DubUrl.Mapping;

namespace DubUrl.ProviderPack.TestingModule;

public sealed class TestProviderModule : IDubUrlProviderModule
{
    public void Register(IProviderCatalogBuilder catalog)
        => ArgumentNullException.ThrowIfNull(catalog);
}
