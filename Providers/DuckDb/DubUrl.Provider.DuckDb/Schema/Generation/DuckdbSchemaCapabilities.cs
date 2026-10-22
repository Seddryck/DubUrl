using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.DuckDb.Schema.Generation;

internal static class DuckdbSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.DuckDb,
            IdentityColumns = IdentityColumnSyntax.DuckDbSequence,
            GeneratedColumns = GeneratedColumnSyntax.DuckDb,
            RegularExpressions = RegularExpressionSyntax.RegexpMatchesFunction,
            SupportsComments = true,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["JSON", "UUID", "HUGEINT"], StringComparer.OrdinalIgnoreCase),
        };
}
