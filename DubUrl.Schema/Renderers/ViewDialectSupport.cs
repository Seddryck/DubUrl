using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;

internal static class ViewDialectSupport
{
    public static void EnsureSupported(IDialect dialect, string operation)
    {
        if ((dialect.Capabilities ?? DialectCapabilities.Default).SupportsViews)
            return;

        throw new NotSupportedException(
            $"Database views are not supported for {operation} by dialect '{dialect.GetType().Name}'.");
    }
}
