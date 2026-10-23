using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Sqlite.Schema.Generation;

internal static class SqliteSchemaCapabilities
{
    internal static DialectCapabilities Create()
        => new()
        {
            ObjectNames = ObjectNameQualification.Sqlite,
            IdentityColumns = IdentityColumnSyntax.SqliteTableConstraint,
            GeneratedColumns = GeneratedColumnSyntax.MySql,
            SupportsViews = true,
            NativeTypes = new HashSet<string>(["TEXT", "BLOB", "INTEGER", "REAL", "NUMERIC"], StringComparer.OrdinalIgnoreCase),
        };
}
