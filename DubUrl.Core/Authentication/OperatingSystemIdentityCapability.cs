using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Authentication;

/// <summary>Configures a provider to connect with credentials from the operating system.</summary>
public interface IOperatingSystemIdentityAdapter
{
    /// <summary>Validates an optional database username without acquiring credentials.</summary>
    void ValidateUsername(string? username);

    /// <summary>Validates a provider-owned option and returns true when it handles that option.
    /// The name is normalized to lowercase letters and digits. Return false to use Core validation.
    /// </summary>
    bool ValidateOption(string name, string value);

    /// <summary>Validates the selected driver and configures the mapped connection string.</summary>
    void Configure(DbConnectionStringBuilder builder);
}

/// <summary>Registers an operating-system identity adapter for a provider route.</summary>
public sealed class OperatingSystemIdentityCapability(IOperatingSystemIdentityAdapter adapter)
{
    internal IOperatingSystemIdentityAdapter Adapter { get; } = adapter ?? throw new ArgumentNullException(nameof(adapter));
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OperatingSystemIdentityAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type adapterType) : Attribute
{
    internal OperatingSystemIdentityCapability Create()
        => new(Activator.CreateInstance(adapterType) as IOperatingSystemIdentityAdapter
            ?? throw new InvalidOperationException("The operating-system identity adapter must implement IOperatingSystemIdentityAdapter."));
}
