using DubUrl.Querying.Dialects;

namespace DubUrl.Schema.Renderers;

internal static class DatabaseObjectNameFormatter
{
    public static string Render(object? value, IDialect dialect)
    {
        if (value is not DatabaseObjectName identity)
            throw new ArgumentException("The object-name formatter requires a structured database object name.", nameof(value));

        var components = dialect switch
        {
            TSqlDialect => new[] { identity.Catalog, identity.Database, identity.Schema, identity.Name },
            PgsqlDialect or CockRoachDialect or CrateDbDialect or QuestDbDialect
                => PostgreSqlComponents(identity, dialect),
            MySqlDialect or SingleStoreDialect => MySqlComponents(identity, dialect),
            DuckDbDialect => DuckDbComponents(identity, dialect),
            SqliteDialect => SqliteComponents(identity, dialect),
            _ => AnsiComponents(identity, dialect)
        };

        return string.Join('.', components.Where(component => component is not null)
            .Select(component => dialect.Renderer.Render(component, "identity")));
    }

    private static string?[] PostgreSqlComponents(DatabaseObjectName identity, IDialect dialect)
    {
        Reject(identity.Catalog, nameof(identity.Catalog), dialect);
        Reject(identity.Database, nameof(identity.Database), dialect);
        return [identity.Schema, identity.Name];
    }

    private static string?[] MySqlComponents(DatabaseObjectName identity, IDialect dialect)
    {
        Reject(identity.Catalog, nameof(identity.Catalog), dialect);
        Reject(identity.Schema, nameof(identity.Schema), dialect);
        return [identity.Database, identity.Name];
    }

    private static string?[] DuckDbComponents(DatabaseObjectName identity, IDialect dialect)
    {
        Reject(identity.Catalog, nameof(identity.Catalog), dialect);
        return [identity.Database, identity.Schema, identity.Name];
    }

    private static string?[] SqliteComponents(DatabaseObjectName identity, IDialect dialect)
    {
        Reject(identity.Catalog, nameof(identity.Catalog), dialect);
        Reject(identity.Schema, nameof(identity.Schema), dialect);
        return [identity.Database, identity.Name];
    }

    private static string?[] AnsiComponents(DatabaseObjectName identity, IDialect dialect)
    {
        Reject(identity.Catalog, nameof(identity.Catalog), dialect);
        Reject(identity.Database, nameof(identity.Database), dialect);
        return [identity.Schema, identity.Name];
    }

    private static void Reject(string? component, string componentName, IDialect dialect)
    {
        if (component is not null)
            throw new NotSupportedException(
                $"Qualifier '{componentName}' is not supported by dialect '{dialect.GetType().Name}'.");
    }
}
