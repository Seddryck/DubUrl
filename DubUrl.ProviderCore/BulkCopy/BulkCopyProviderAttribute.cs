namespace DubUrl.BulkCopy;

/// <summary>Declares a native mapper's bulk provider for attribute-based and generated connector registration.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public abstract class BulkCopyProviderAttribute : Attribute
{
    public abstract IBulkCopyProvider Create();
}

public sealed class BulkCopyProviderAttribute<TProvider> : BulkCopyProviderAttribute
    where TProvider : IBulkCopyProvider, new()
{
    public override IBulkCopyProvider Create() => new TProvider();
}
