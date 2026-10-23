using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.PostgreSql.Schema.Generation;

internal static class PgsqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameParts.Name | ObjectNameParts.Schema,
            SupportsIdentityColumns = true,
            SupportsStoredGeneratedColumns = true,
            SupportsRegularExpressionChecks = true,
            SupportsComments = true,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["JSON", "JSONB", "UUID", "TEXT", "CITEXT"], StringComparer.OrdinalIgnoreCase),
        };
}
