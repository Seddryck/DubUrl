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
            NativeTypes = new HashSet<string>(["JSON", "UUID", "HUGEINT"], StringComparer.OrdinalIgnoreCase),
        };
}
