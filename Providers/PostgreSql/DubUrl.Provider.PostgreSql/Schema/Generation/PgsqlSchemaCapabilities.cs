using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.PostgreSql.Schema.Generation;

internal static class PgsqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.PostgreSql,
            IdentityColumns = IdentityColumnSyntax.PostgreSql,
            GeneratedColumns = GeneratedColumnSyntax.PostgreSql,
            RegularExpressions = RegularExpressionSyntax.TildeOperator,
            SupportsComments = true,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["JSON", "JSONB", "UUID", "TEXT", "CITEXT"], StringComparer.OrdinalIgnoreCase),
        };
}
