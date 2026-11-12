using System.Diagnostics.CodeAnalysis;

namespace DubUrl.BulkCopy;

/// <summary>Loads existing files into a table using a provider's file-loading operation.</summary>
public interface IFileLoader
{
    void Load(ConnectionUrl connectionUrl, string tableName, string sourcePath, string fileFormat);
}

/// <summary>Registers a provider's file loader through ProviderRegistration.WithCapabilities.</summary>
public sealed record FileLoadCapability(IFileLoader Loader);

/// <summary>Registers a file loader on an attributed provider mapper.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FileLoadAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type loaderType) : Attribute
{
    internal FileLoadCapability Create() => new((IFileLoader)(Activator.CreateInstance(loaderType)
        ?? throw new InvalidOperationException("A file loader must have a public parameterless constructor.")));
}
