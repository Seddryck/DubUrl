using DubUrl.Querying.Dialects;

namespace DubUrl;

/// <summary>A literal table or view name bound to a database URL.</summary>
public sealed partial class TableTarget
{
    private readonly DatabaseUrl _database;
    private readonly string _sqlName;

    public string Name { get; }
    public string? Schema { get; }
    public string? Database { get; }
    public string? Catalog { get; }

    internal TableTarget(DatabaseUrl database, string name, string? schema, string? databaseName, string? catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (schema is not null) ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (databaseName is not null) ArgumentException.ThrowIfNullOrWhiteSpace(databaseName, "database");
        if (catalog is not null) ArgumentException.ThrowIfNullOrWhiteSpace(catalog);

        _database = database;
        (Name, Schema, Database, Catalog) = (name, schema, databaseName, catalog);
        _sqlName = _database.Dialect.RenderTableName(new SchemaSqlObjectName(Name, Schema, Database, Catalog));
    }

    internal string SqlName => _sqlName;
}
