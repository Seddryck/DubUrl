using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;

internal static class ViewDialectSupport
{
    public static void EnsureSupported(IDialect dialect, string operation)
    {
        if (dialect is TSqlDialect or PgsqlDialect or CockRoachDialect or MySqlDialect
            or SingleStoreDialect or SqliteDialect or DuckDbDialect)
            return;

        throw new NotSupportedException(
            $"Database views are not supported for {operation} by dialect '{dialect.GetType().Name}'.");
    }
}
