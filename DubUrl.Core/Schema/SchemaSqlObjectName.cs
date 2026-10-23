namespace DubUrl.Querying.Dialects;

/// <summary>Literal identity components supplied to provider-owned schema SQL rendering.</summary>
public sealed record SchemaSqlObjectName(string Name, string? Schema = null, string? Database = null, string? Catalog = null);
