using System;

namespace DubUrl.Mapping;

/// <summary>
/// Builds an explicit, deterministic catalog of DubUrl providers.
/// </summary>
public interface IProviderCatalogBuilder
{
    IProviderCatalogBuilder AddProvider(ProviderRegistration registration);
    IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum;
}
