using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.SqlServer.Schema.Generation;

internal static class TSqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.FourPart,
            IdentityColumns = IdentityColumnSyntax.SqlServer,
            GeneratedColumns = GeneratedColumnSyntax.SqlServer,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["XML", "UNIQUEIDENTIFIER", "GEOGRAPHY", "GEOMETRY"], StringComparer.OrdinalIgnoreCase),
        };
}
