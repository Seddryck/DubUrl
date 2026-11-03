namespace DubUrl.Querying.Dialects;

public enum SchemaTableStorage { Default, Rowstore, Columnstore }
public enum SchemaIndexStructure { Default, Ordered, Hash }
public sealed record SchemaIndexColumn(string Name, bool? Descending = null);
public sealed record SchemaIndexDefinition(string Name, IReadOnlyList<SchemaIndexColumn> Columns,
    bool Unique = false, SchemaIndexStructure Structure = SchemaIndexStructure.Default);
public sealed record SchemaKeyDefinition(string? Name, IReadOnlyList<string> Columns);

/// <summary>Semantic physical choices. Null shard columns means engine inference; an empty list means keyless.</summary>
public sealed record TablePhysicalOptions(SchemaTableStorage Storage = SchemaTableStorage.Default,
    IReadOnlyList<string>? ShardKey = null, IReadOnlyList<SchemaIndexColumn>? SortKey = null);

/// <summary>Resolved table inventory, including all inline and table-level unique/primary keys.</summary>
public sealed record TableDefinitionContext(SchemaSqlObjectName Table, IReadOnlyList<string> Columns,
    SchemaKeyDefinition? PrimaryKey = null, IReadOnlyList<SchemaKeyDefinition>? UniqueKeys = null,
    TablePhysicalOptions? Options = null, IReadOnlyList<SchemaIndexDefinition>? Indexes = null);

/// <summary>Provider-owned CREATE prefix and definitions appended inside the parentheses.</summary>
/// <remarks>Consumes all primary/unique key and index definitions. Consumers must omit their portable equivalents,
/// including inline column keys. Append description options independently, after the closing parenthesis.</remarks>
public sealed record TableDefinitionPlan(string CreatePrefix, IReadOnlyList<string> Definitions);

public sealed record IndexDefinitionContext(SchemaSqlObjectName Index, TableDefinitionContext Table,
    IReadOnlyList<SchemaIndexColumn> Columns, bool Unique = false,
    SchemaIndexStructure Structure = SchemaIndexStructure.Default);

/// <summary>Optional provider-owned table/key and complete index rendering. All names are literal identifiers.</summary>
/// <remarks>Consumers must validate options even when no explicit index exists. Never fall back to portable
/// templates when semantic options are present and the dialect does not implement this contract.</remarks>
public interface ITableDefinitionSqlRenderer
{
    TableDefinitionPlan RenderTableDefinition(IDialect dialect, TableDefinitionContext table);
    string RenderCreateIndex(IDialect dialect, IndexDefinitionContext index);
}
