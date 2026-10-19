using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.PostgreSql.Schema.Generation;

internal static class QuestDbSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.PostgreSql,
            RegularExpressions = RegularExpressionSyntax.TildeOperator,
        };
}
