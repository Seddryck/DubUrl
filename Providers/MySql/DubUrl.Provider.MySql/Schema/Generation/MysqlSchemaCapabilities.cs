using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal static class MysqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameParts.Name | ObjectNameParts.Database,
            SupportsIdentityColumns = true,
            SupportsStoredGeneratedColumns = true,
            SupportsVirtualGeneratedColumns = true,
            SupportsRegularExpressionChecks = true,
            SupportsViews = true,
            SupportsComments = true,
            NativeTypes = new HashSet<string>(["JSON", "GEOMETRY", "VARCHAR", "CHAR", "TEXT", "BLOB"], StringComparer.OrdinalIgnoreCase),
        };
}
