using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal static class SingleStoreSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameParts.Name | ObjectNameParts.Database,
            SupportsIdentityColumns = true,
            SupportsStoredGeneratedColumns = true,
            SupportsVirtualGeneratedColumns = false,
            SupportsRegularExpressionChecks = true,
            SupportsViews = true,
            SupportsComments = true,
            NativeTypes = new HashSet<string>(["JSON", "GEOGRAPHYPOINT", "GEOGRAPHY", "CHAR", "VARCHAR"], StringComparer.OrdinalIgnoreCase),
        };
}
