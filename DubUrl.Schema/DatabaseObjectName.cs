namespace DubUrl.Schema;

public sealed record DatabaseObjectName
{
    public string? Catalog { get; }
    public string? Database { get; }
    public string? Schema { get; }
    public string Name { get; }

    public string Key => Catalog is null && Database is null && Schema is null
        ? Name
        : string.Join('\u001f', Catalog ?? "<null>", Database ?? "<null>", Schema ?? "<null>", Name);

    public DatabaseObjectName(string name, string? schema = null, string? database = null, string? catalog = null)
    {
        Name = ValidateRequired(name, nameof(name));
        Schema = ValidateOptional(schema, nameof(schema));
        Database = ValidateOptional(database, nameof(database));
        Catalog = ValidateOptional(catalog, nameof(catalog));
    }

    public override string ToString()
        => string.Join('.', new[] { Catalog, Database, Schema, Name }.Where(part => part is not null));

    private static string ValidateRequired(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("An object name must be provided.", parameterName)
            : value;

    private static string? ValidateOptional(string? value, string parameterName)
        => value is null ? null : string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Qualifier components cannot be empty or whitespace.", parameterName)
            : value;
}
