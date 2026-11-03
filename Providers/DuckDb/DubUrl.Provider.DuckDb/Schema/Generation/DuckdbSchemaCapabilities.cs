using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.DuckDb.Schema.Generation;

internal static class DuckdbSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameParts.Name | ObjectNameParts.Schema | ObjectNameParts.Database,
            SupportsIdentityColumns = true,
            SupportsVirtualGeneratedColumns = true,
            SupportsRegularExpressionChecks = true,
            SupportsComments = true,
            SupportsViews = true,
            // CHAR is a VARCHAR alias; DuckDB accepts but does not enforce declared lengths.
            NativeTypes = new HashSet<string>(["JSON", "UUID", "HUGEINT", "CHAR", "VARCHAR"], StringComparer.OrdinalIgnoreCase),
        };
}
