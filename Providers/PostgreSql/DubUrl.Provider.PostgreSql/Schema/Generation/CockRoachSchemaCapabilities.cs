using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.PostgreSql.Schema.Generation;

internal static class CockRoachSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.PostgreSql,
            IdentityColumns = IdentityColumnSyntax.PostgreSql,
            GeneratedColumns = GeneratedColumnSyntax.CockroachDb,
            RegularExpressions = RegularExpressionSyntax.TildeOperator,
            SupportsComments = true,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["JSON", "JSONB", "UUID"], StringComparer.OrdinalIgnoreCase),
        };
}
