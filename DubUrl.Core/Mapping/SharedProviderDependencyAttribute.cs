namespace DubUrl.Mapping;

/// <summary>
/// Marks a public API dependency that installed providers must share with the host.
/// Only an already loaded host assembly with the exact requested identity is shared.
/// Driver implementation dependencies remain isolated.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SharedProviderDependencyAttribute(Type contractType) : Attribute
{
    public Type ContractType { get; } = contractType ?? throw new ArgumentNullException(nameof(contractType));
}
