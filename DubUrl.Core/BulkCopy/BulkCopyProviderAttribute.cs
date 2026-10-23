namespace DubUrl.BulkCopy;

/// <summary>Declares an optional connector adapter without linking its driver into a provider family.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BulkCopyProviderAttribute(string assemblyName, string typeName) : Attribute
{
    public IBulkCopyProvider Create() => new LazyBulkCopyProvider(assemblyName, typeName);
}
