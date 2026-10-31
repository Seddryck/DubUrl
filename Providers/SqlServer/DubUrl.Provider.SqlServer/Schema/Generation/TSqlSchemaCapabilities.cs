using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.SqlServer.Schema.Generation;

internal static class TSqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameParts.Name | ObjectNameParts.Schema | ObjectNameParts.Database | ObjectNameParts.Catalog,
            SupportsIdentityColumns = true,
            SupportsStoredGeneratedColumns = true,
            SupportsVirtualGeneratedColumns = true,
            SupportsViews = true,
            SupportsComments = true,
            NativeTypes = new HashSet<string>(["VARCHAR", "NVARCHAR", "CHAR", "NCHAR", "XML", "UNIQUEIDENTIFIER", "GEOGRAPHY", "GEOMETRY"], StringComparer.OrdinalIgnoreCase),
        };
}
