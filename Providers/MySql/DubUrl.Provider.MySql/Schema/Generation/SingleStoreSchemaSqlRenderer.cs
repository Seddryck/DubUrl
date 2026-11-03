using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.MySql.Schema.Generation;

internal sealed class SingleStoreSchemaSqlRenderer : StandardSchemaSqlRenderer, ITableDescriptionSqlRenderer, ITableDefinitionSqlRenderer
{
    internal static SingleStoreSchemaSqlRenderer Default { get; } = new();
    private SingleStoreSchemaSqlRenderer() : base(MySqlSchemaSqlRenderer.Default) { }

    public string RenderTableDescription(IDialect dialect, SchemaSqlObjectName table, string description)
    {
        _ = RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        return " COMMENT = '" + description.Replace("\\", "\\\\").Replace("'", "''") + "'";
    }

    public override string RenderComment(IDialect dialect, SchemaSqlObjectName table, string? column, string description)
        => column is null
            ? throw new NotSupportedException("SingleStore table descriptions require ITableDescriptionSqlRenderer during CREATE TABLE; ALTER TABLE COMMENT does not persist them on the supported engine.")
            : string.Empty;

    public override string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
        => stored ? $"AS ({expression}) PERSISTED {type}"
            : throw new NotSupportedException("SingleStore supports persisted computed columns only; virtual generation is not supported.");

    public TableDefinitionPlan RenderTableDefinition(IDialect dialect, TableDefinitionContext table)
    {
        _ = RenderObjectName(dialect, table.Table.Catalog, table.Table.Database, table.Table.Schema, table.Table.Name);
        var options = table.Options ?? new();
        ValidateTable(table, options);
        var rowstore = options.Storage == SchemaTableStorage.Rowstore;
        var definitions = RenderKeys(dialect, table, rowstore);
        definitions.Add("SHARD KEY (" + Names(dialect, ShardKey(table)) + ")");
        if (!rowstore) definitions.Add("SORT KEY (" + OrderedNames(dialect, options.SortKey ?? []) + ")");
        foreach (var index in table.Indexes ?? [])
            definitions.Add(RenderIndexDefinition(dialect, new(new(index.Name), table, index.Columns, index.Unique, index.Structure)));
        return new(rowstore ? "CREATE ROWSTORE TABLE" : "CREATE TABLE", definitions);
    }

    private static void ValidateTable(TableDefinitionContext table, TablePhysicalOptions options)
    {
        if (!Enum.IsDefined(options.Storage)) throw new ArgumentException("Unknown table storage.");
        ValidateColumns(table.Columns, table.Columns, "table");
        ValidateColumns(table.Columns, ShardKey(table), "shard key", allowEmpty: true);
        var keys = AllUniqueKeys(table).ToArray();
        var rowstore = options.Storage == SchemaTableStorage.Rowstore;
        if (!rowstore && keys.Length > 1)
            throw new NotSupportedException("SingleStore columnstore supports only one primary/unique key; select rowstore for multiple unique keys.");
        foreach (var key in keys)
        {
            ValidateColumns(table.Columns, key.Columns, "unique key");
            ValidateShard(ShardKey(table), key.Columns);
        }
        if (rowstore && options.SortKey is not null)
            throw new NotSupportedException("SingleStore SORT KEY requires columnstore storage; use an ordered rowstore index instead.");
        ValidateColumns(table.Columns, (options.SortKey ?? []).Select(column => column.Name).ToArray(), "sort key", allowEmpty: true);
    }

    private static IEnumerable<SchemaKeyDefinition> AllUniqueKeys(TableDefinitionContext table)
    {
        if (table.PrimaryKey is { } primary) yield return primary;
        foreach (var key in table.UniqueKeys ?? []) yield return key;
        foreach (var index in table.Indexes ?? [])
            if (index.Unique) yield return new(index.Name, index.Columns.Select(column => column.Name).ToArray());
    }

    private static List<string> RenderKeys(IDialect dialect, TableDefinitionContext table, bool rowstore)
    {
        var definitions = new List<string>();
        var structure = rowstore ? "" : " USING HASH";
        if (table.PrimaryKey is { } primary)
            definitions.Add("PRIMARY KEY (" + Names(dialect, primary.Columns) + ")" + structure);
        foreach (var key in table.UniqueKeys ?? [])
            definitions.Add("UNIQUE KEY " + (key.Name is null ? "" : Quote(dialect, key.Name) + " ") +
                "(" + Names(dialect, key.Columns) + ")" + structure);
        return definitions;
    }

    public string RenderCreateIndex(IDialect dialect, IndexDefinitionContext index)
    {
        _ = RenderTableDefinition(dialect, index.Table);
        if (index.Unique)
            throw new NotSupportedException("SingleStore unique indexes must be declared during CREATE TABLE using TableDefinitionContext.Indexes; adding one online is unsupported.");
        var definition = RenderIndexDefinition(dialect, index);
        var table = index.Table.Table;
        var tableName = RenderObjectName(dialect, table.Catalog, table.Database, table.Schema, table.Name);
        var name = Quote(dialect, index.Index.Name);
        return "CREATE INDEX " + name + " ON " + tableName + definition[("KEY " + name).Length..] + ";";
    }

    private string RenderIndexDefinition(IDialect dialect, IndexDefinitionContext index)
    {
        var structure = ResolveStructure(index);
        ValidateColumns(index.Table.Columns, index.Columns.Select(column => column.Name).ToArray(), "index");
        ValidateIndexStructure(index, structure);
        ValidateIndexDatabase(index);
        if (index.Unique) ValidateShard(ShardKey(index.Table), index.Columns.Select(column => column.Name).ToArray());
        var unique = index.Unique ? "UNIQUE " : "";
        var nativeStructure = structure == SchemaIndexStructure.Hash ? "HASH" : "BTREE";
        return $"{unique}KEY {Quote(dialect, index.Index.Name)} ({OrderedNames(dialect, index.Columns)}) USING {nativeStructure}";
    }

    private static SchemaIndexStructure ResolveStructure(IndexDefinitionContext index)
    {
        if (!Enum.IsDefined(index.Structure)) throw new ArgumentException("Unknown index structure.");
        if (index.Structure != SchemaIndexStructure.Default) return index.Structure;
        return index.Table.Options?.Storage == SchemaTableStorage.Rowstore ? SchemaIndexStructure.Ordered : SchemaIndexStructure.Hash;
    }

    private static void ValidateIndexStructure(IndexDefinitionContext index, SchemaIndexStructure structure)
    {
        if (structure == SchemaIndexStructure.Ordered && index.Table.Options?.Storage != SchemaTableStorage.Rowstore)
            throw new NotSupportedException("SingleStore ordered indexes require rowstore storage; use a columnstore SORT KEY for ordered storage.");
        if (structure == SchemaIndexStructure.Hash && index.Columns.Any(column => column.Descending.HasValue))
            throw new NotSupportedException("SingleStore hash indexes cannot represent ASC/DESC direction.");
    }

    private static void ValidateIndexDatabase(IndexDefinitionContext index)
    {
        if (index.Index.Catalog is not null || index.Index.Schema is not null ||
            (index.Index.Database is not null && index.Index.Database != index.Table.Table.Database))
            throw new NotSupportedException("An index must belong to its table's database.");
    }

    private static IReadOnlyList<string> ShardKey(TableDefinitionContext table) => table.Options?.ShardKey ?? table.PrimaryKey?.Columns ?? [];
    private static string Quote(IDialect dialect, string name) => dialect.Renderer.Render(name, "identity");
    private static string Names(IDialect dialect, IEnumerable<string> columns) => string.Join(", ", columns.Select(column => Quote(dialect, column)));
    private static string OrderedNames(IDialect dialect, IEnumerable<SchemaIndexColumn> columns)
        => string.Join(", ", columns.Select(column => Quote(dialect, column.Name) + Direction(column.Descending)));
    private static string Direction(bool? descending) => descending switch { true => " DESC", false => " ASC", null => "" };

    private static void ValidateColumns(IReadOnlyList<string> inventory, IReadOnlyList<string> columns, string kind, bool allowEmpty = false)
    {
        if ((!allowEmpty && columns.Count == 0) || columns.Any(string.IsNullOrWhiteSpace) ||
            columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count ||
            columns.Any(column => !inventory.Contains(column, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException($"Invalid or unknown columns in {kind}.");
    }

    private static void ValidateShard(IReadOnlyList<string> shard, IReadOnlyList<string> unique)
    {
        if (shard.Count == 0 || shard.Any(column => !unique.Contains(column, StringComparer.OrdinalIgnoreCase)))
            throw new NotSupportedException("SingleStore enforced uniqueness requires a nonempty shard key contained in every primary/unique key.");
    }
}
