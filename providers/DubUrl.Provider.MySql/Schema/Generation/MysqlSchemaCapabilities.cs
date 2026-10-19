using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal static class MysqlSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.MySql,
            IdentityColumns = IdentityColumnSyntax.MySql,
            GeneratedColumns = GeneratedColumnSyntax.MySql,
            RegularExpressions = RegularExpressionSyntax.RegexpOperator,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["JSON", "GEOMETRY"], StringComparer.OrdinalIgnoreCase),
        };
}
