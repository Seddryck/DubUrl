using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.Application;

namespace DubUrl.Core.Testing;

internal static class ConnectorFixture
{
    public static AttributeProviderModule Create(string provider)
    {
        using var stream = typeof(BundledProviderCatalog).Assembly.GetManifestResourceStream("DubUrl.ConnectorCatalog")!;
        var definition = ProviderPackCatalogDocument.Read(stream).Entries.Single(entry => entry.Provider == provider);
        return AttributeProviderModule.FromConnector(definition, path => Assembly.Load(Path.GetFileNameWithoutExtension(path)));
    }
}
