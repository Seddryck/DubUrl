using System.Reflection;
using DubUrl.Querying.Dialects;

namespace DubUrl.Mapping;

/// <summary>Registers providers from the attributes in the bundled implementation assemblies.</summary>
public sealed class BuiltInProviderModule : IDubUrlProviderModule
{
    private static Assembly[] Assemblies() => AssemblyTypesProbe.GetProviderAssemblies(typeof(BuiltInProviderModule).Assembly);

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Provider discovery reads attributes and mapper constructors.")]
    public void Register(IProviderCatalogBuilder catalog)
        => new AttributeProviderModule(Assemblies()).Register(catalog);

    public static IDialectRegistry GetDialectRegistry()
        => new ProviderCatalogBuilder().AddModule(new BuiltInProviderModule()).BuildDialects();
}
