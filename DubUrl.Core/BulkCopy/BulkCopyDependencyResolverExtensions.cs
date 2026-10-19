using System.Reflection;

namespace DubUrl.BulkCopy;

/// <summary>Scopes lazy resolution and reports missing dependencies for a native bulk operation.</summary>
public static class BulkCopyDependencyResolverExtensions
{
    public static void Execute(this IBulkCopyDependencyResolver dependencies, Assembly driverAssembly, Action operation)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(operation);
        using var scope = dependencies.EnterScope(driverAssembly);
        try
        {
            operation();
        }
        catch (Exception exception) when (FindMissingDependency(exception) is not null)
        {
            var missing = FindMissingDependency(exception)!;
            throw new FileNotFoundException(
                $"Optional bulk-copy dependency '{missing.FileName}' could not be loaded. " +
                "Deploy the DLL and its dependencies with the connector, or pass a BulkCopyDependencyResolver " +
                "with their search directories to BulkCopy. No compile-time reference is required.", missing.FileName, exception);
        }
    }

    private static FileNotFoundException? FindMissingDependency(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is FileNotFoundException missing)
                return missing.Message.StartsWith("Optional bulk-copy dependency", StringComparison.Ordinal) ? null : missing;
        return null;
    }
}
