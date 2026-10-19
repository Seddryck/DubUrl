using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.PostgreSql.Schema.Generation;

internal static class CrateDbSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.PostgreSql,
            RegularExpressions = RegularExpressionSyntax.TildeOperator,
            SupportsComments = true,
            NativeTypes = new HashSet<string>(["JSON", "OBJECT", "GEO_POINT", "GEO_SHAPE"], StringComparer.OrdinalIgnoreCase),
        };
}
