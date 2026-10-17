using System.Collections.ObjectModel;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

public interface IValidationRule
{
    string Id { get; }
}

public interface IValidationRule<in TContext> : IValidationRule
{
    void Validate(TContext context, ICollection<SchemaDocumentDiagnostic> diagnostics);
}

public sealed class ColumnValidationContext
{
    internal SchemaDocumentValueConverter ValueConverter { get; }

    public ColumnDocument Column { get; }
    public string Path { get; }
    public IReadOnlyList<ColumnDocument> Columns { get; }

    public ColumnValidationContext(ColumnDocument column, string path, IReadOnlyList<ColumnDocument> columns)
        : this(column, path, columns,
            new SchemaDocumentValueConverter(SchemaDocumentLiteralConverterRegistry.CreateDefault()))
    { }

    internal ColumnValidationContext(
        ColumnDocument column,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        SchemaDocumentValueConverter valueConverter)
    {
        Column = column;
        Path = path;
        Columns = columns;
        ValueConverter = valueConverter;
    }
}

public sealed record ForeignKeyValidationContext(
    ForeignKeyDocument ForeignKey,
    string Path,
    IReadOnlyDictionary<string, ColumnDocument> SourceColumns,
    TableDocument? TargetTable);

public sealed class IndexValidationContext
{
    internal ISet<string> DeclaredIdentities { get; }

    public IndexDocument Index { get; }
    public string Path { get; }
    public TableDocument? Table { get; }

    public IndexValidationContext(IndexDocument index, string path, TableDocument? table)
        : this(index, path, table, new HashSet<string>(StringComparer.Ordinal))
    { }

    internal IndexValidationContext(
        IndexDocument index,
        string path,
        TableDocument? table,
        ISet<string> declaredIdentities)
    {
        Index = index;
        Path = path;
        Table = table;
        DeclaredIdentities = declaredIdentities;
    }
}

public sealed class CheckValidationContext
{
    internal SchemaDocumentValueConverter ValueConverter { get; }

    public CheckDocument Check { get; }
    public string Path { get; }
    public IReadOnlyList<ColumnDocument> Columns { get; }

    public CheckValidationContext(CheckDocument check, string path, IReadOnlyList<ColumnDocument> columns)
        : this(check, path, columns,
            new SchemaDocumentValueConverter(SchemaDocumentLiteralConverterRegistry.CreateDefault()))
    { }

    internal CheckValidationContext(
        CheckDocument check,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        SchemaDocumentValueConverter valueConverter)
    {
        Check = check;
        Path = path;
        Columns = columns;
        ValueConverter = valueConverter;
    }
}

public sealed record ViewDeclarationValidationContext(
    ViewDocument View,
    string Path,
    bool IsDuplicate,
    bool ConflictsWithTable);

public sealed class ViewValidationContext
{
    internal SchemaDocumentValueConverter ValueConverter { get; }
    internal bool HasResolvedSource => SourceTable is not null || SourceView is not null;

    public ViewDocument View { get; }
    public string Path { get; }
    public TableDocument? SourceTable { get; }
    public ViewDocument? SourceView { get; }
    public IReadOnlyList<ColumnDocument> SourceColumns { get; }

    public ViewValidationContext(
        ViewDocument view,
        string path,
        TableDocument? sourceTable,
        ViewDocument? sourceView,
        IReadOnlyList<ColumnDocument> sourceColumns)
        : this(view, path, sourceTable, sourceView, sourceColumns,
            new SchemaDocumentValueConverter(SchemaDocumentLiteralConverterRegistry.CreateDefault()))
    { }

    internal ViewValidationContext(
        ViewDocument view,
        string path,
        TableDocument? sourceTable,
        ViewDocument? sourceView,
        IReadOnlyList<ColumnDocument> sourceColumns,
        SchemaDocumentValueConverter valueConverter)
    {
        View = view;
        Path = path;
        SourceTable = sourceTable;
        SourceView = sourceView;
        SourceColumns = sourceColumns;
        ValueConverter = valueConverter;
    }
}

public sealed class ViewGraphValidationContext
{
    internal IReadOnlyDictionary<string, (ViewDocument View, int Index)> ViewLookup { get; }

    public IReadOnlyList<ViewDocument> Views { get; }

    public ViewGraphValidationContext(IReadOnlyList<ViewDocument> views)
        : this(views, CreateLookup(views))
    { }

    internal ViewGraphValidationContext(
        IReadOnlyList<ViewDocument> views,
        IReadOnlyDictionary<string, (ViewDocument View, int Index)> viewLookup)
    {
        Views = views;
        ViewLookup = viewLookup;
    }

    private static IReadOnlyDictionary<string, (ViewDocument View, int Index)> CreateLookup(
        IReadOnlyList<ViewDocument> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        var lookup = new Dictionary<string, (ViewDocument View, int Index)>(StringComparer.Ordinal);
        for (var index = 0; index < views.Count; index++)
        {
            var view = views[index];
            if (view is null || !TryIdentityKey(view.Identity, out var key))
                continue;
            lookup.TryAdd(key, (view, index));
        }
        return new ReadOnlyDictionary<string, (ViewDocument View, int Index)>(lookup);
    }

    private static bool TryIdentityKey(DatabaseObjectNameDocument? identity, out string key)
    {
        key = string.Empty;
        if (identity is null || string.IsNullOrWhiteSpace(identity.Name)
            || new[] { identity.Catalog, identity.Database, identity.Schema }
                .Any(component => component is not null && string.IsNullOrWhiteSpace(component)))
            return false;
        key = string.Join('\u001f', identity.Catalog ?? "<null>", identity.Database ?? "<null>",
            identity.Schema ?? "<null>", identity.Name);
        return true;
    }
}

public static class SchemaDocumentValidationRuleIds
{
    public static class Columns
    {
        public const string LogicalType = "column.logical-type";
        public const string Length = "column.length";
        public const string PrecisionScale = "column.precision-scale";
        public const string ShapeExclusivity = "column.shape-exclusivity";
        public const string GenerationExclusivity = "column.generation-exclusivity";
        public const string Identity = "column.identity";
        public const string Generated = "column.generated";
        public const string DefaultValue = "column.default-value";
        public const string NativeType = "column.native-type";
    }

    public static class ForeignKeys
    {
        public const string Name = "foreign-key.name";
        public const string SourceColumns = "foreign-key.source-columns";
        public const string TargetTable = "foreign-key.target-table";
        public const string TargetColumnsPresence = "foreign-key.target-columns-presence";
        public const string Arity = "foreign-key.arity";
        public const string TargetColumns = "foreign-key.target-columns";
    }

    public static class Indexes
    {
        public const string Identity = "index.identity";
        public const string Table = "index.table";
        public const string Columns = "index.columns";
    }

    public static class Checks
    {
        public const string Kind = "check.kind";
        public const string Comparison = "check.comparison";
        public const string Membership = "check.membership";
        public const string Regex = "check.regex";
    }

    public static class ViewDeclarations
    {
        public const string Identity = "view-declaration.identity";
    }

    public static class Views
    {
        public const string Query = "view.query";
        public const string Source = "view.source";
        public const string Projections = "view.projections";
    }

    public static class ViewGraph
    {
        public const string DependencyCycles = "view-graph.dependency-cycles";
    }
}

public sealed class SchemaDocumentValidationRuleSet
{
    public IReadOnlyList<IValidationRule<ColumnValidationContext>> ColumnRules { get; }
    public IReadOnlyList<IValidationRule<ForeignKeyValidationContext>> ForeignKeyRules { get; }
    public IReadOnlyList<IValidationRule<IndexValidationContext>> IndexRules { get; }
    public IReadOnlyList<IValidationRule<CheckValidationContext>> CheckRules { get; }
    public IReadOnlyList<IValidationRule<ViewDeclarationValidationContext>> ViewDeclarationRules { get; }
    public IReadOnlyList<IValidationRule<ViewValidationContext>> ViewRules { get; }
    public IReadOnlyList<IValidationRule<ViewGraphValidationContext>> ViewGraphRules { get; }

    internal SchemaDocumentValidationRuleSet(
        IEnumerable<IValidationRule<ColumnValidationContext>> columnRules,
        IEnumerable<IValidationRule<ForeignKeyValidationContext>> foreignKeyRules,
        IEnumerable<IValidationRule<IndexValidationContext>> indexRules,
        IEnumerable<IValidationRule<CheckValidationContext>> checkRules,
        IEnumerable<IValidationRule<ViewDeclarationValidationContext>> viewDeclarationRules,
        IEnumerable<IValidationRule<ViewValidationContext>> viewRules,
        IEnumerable<IValidationRule<ViewGraphValidationContext>> viewGraphRules)
    {
        ColumnRules = Snapshot(columnRules);
        ForeignKeyRules = Snapshot(foreignKeyRules);
        IndexRules = Snapshot(indexRules);
        CheckRules = Snapshot(checkRules);
        ViewDeclarationRules = Snapshot(viewDeclarationRules);
        ViewRules = Snapshot(viewRules);
        ViewGraphRules = Snapshot(viewGraphRules);
        EnsureUniqueRuleIds(ColumnRules.Cast<IValidationRule>()
            .Concat(ForeignKeyRules)
            .Concat(IndexRules)
            .Concat(CheckRules)
            .Concat(ViewDeclarationRules)
            .Concat(ViewRules)
            .Concat(ViewGraphRules));
    }

    public static SchemaDocumentValidationRuleSet CreateDefault()
        => SchemaDocumentValidator.CreateDefaultRuleSet();

    public SchemaDocumentValidationRuleSet WithColumnRule(IValidationRule<ColumnValidationContext> rule)
        => With(rule, ColumnRules, (columns) => Copy(columnRules: columns));

    public SchemaDocumentValidationRuleSet WithoutColumnRule(string id)
        => Copy(columnRules: Without(ColumnRules, id));

    public SchemaDocumentValidationRuleSet WithForeignKeyRule(IValidationRule<ForeignKeyValidationContext> rule)
        => With(rule, ForeignKeyRules, (foreignKeys) => Copy(foreignKeyRules: foreignKeys));

    public SchemaDocumentValidationRuleSet WithoutForeignKeyRule(string id)
        => Copy(foreignKeyRules: Without(ForeignKeyRules, id));

    public SchemaDocumentValidationRuleSet WithIndexRule(IValidationRule<IndexValidationContext> rule)
        => With(rule, IndexRules, (indexes) => Copy(indexRules: indexes));

    public SchemaDocumentValidationRuleSet WithoutIndexRule(string id)
        => Copy(indexRules: Without(IndexRules, id));

    public SchemaDocumentValidationRuleSet WithCheckRule(IValidationRule<CheckValidationContext> rule)
        => With(rule, CheckRules, (checks) => Copy(checkRules: checks));

    public SchemaDocumentValidationRuleSet WithoutCheckRule(string id)
        => Copy(checkRules: Without(CheckRules, id));

    public SchemaDocumentValidationRuleSet WithViewDeclarationRule(
        IValidationRule<ViewDeclarationValidationContext> rule)
        => With(rule, ViewDeclarationRules, (views) => Copy(viewDeclarationRules: views));

    public SchemaDocumentValidationRuleSet WithoutViewDeclarationRule(string id)
        => Copy(viewDeclarationRules: Without(ViewDeclarationRules, id));

    public SchemaDocumentValidationRuleSet WithViewRule(IValidationRule<ViewValidationContext> rule)
        => With(rule, ViewRules, (views) => Copy(viewRules: views));

    public SchemaDocumentValidationRuleSet WithoutViewRule(string id)
        => Copy(viewRules: Without(ViewRules, id));

    public SchemaDocumentValidationRuleSet WithViewGraphRule(IValidationRule<ViewGraphValidationContext> rule)
        => With(rule, ViewGraphRules, (views) => Copy(viewGraphRules: views));

    public SchemaDocumentValidationRuleSet WithoutViewGraphRule(string id)
        => Copy(viewGraphRules: Without(ViewGraphRules, id));

    internal SchemaDocumentValidationRuleSet Snapshot()
        => Copy();

    private SchemaDocumentValidationRuleSet Copy(
        IEnumerable<IValidationRule<ColumnValidationContext>>? columnRules = null,
        IEnumerable<IValidationRule<ForeignKeyValidationContext>>? foreignKeyRules = null,
        IEnumerable<IValidationRule<IndexValidationContext>>? indexRules = null,
        IEnumerable<IValidationRule<CheckValidationContext>>? checkRules = null,
        IEnumerable<IValidationRule<ViewDeclarationValidationContext>>? viewDeclarationRules = null,
        IEnumerable<IValidationRule<ViewValidationContext>>? viewRules = null,
        IEnumerable<IValidationRule<ViewGraphValidationContext>>? viewGraphRules = null)
        => new(
            columnRules ?? ColumnRules,
            foreignKeyRules ?? ForeignKeyRules,
            indexRules ?? IndexRules,
            checkRules ?? CheckRules,
            viewDeclarationRules ?? ViewDeclarationRules,
            viewRules ?? ViewRules,
            viewGraphRules ?? ViewGraphRules);

    private static SchemaDocumentValidationRuleSet With<TContext>(
        IValidationRule<TContext> rule,
        IReadOnlyList<IValidationRule<TContext>> rules,
        Func<IEnumerable<IValidationRule<TContext>>, SchemaDocumentValidationRuleSet> copy)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return copy(rules.Append(rule));
    }

    private static IEnumerable<IValidationRule<TContext>> Without<TContext>(
        IReadOnlyList<IValidationRule<TContext>> rules,
        string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return rules.Where(rule => !StringComparer.Ordinal.Equals(rule.Id, id));
    }

    private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return new ReadOnlyCollection<T>(items.ToArray());
    }

    private static void EnsureUniqueRuleIds(IEnumerable<IValidationRule> rules)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentException.ThrowIfNullOrWhiteSpace(rule.Id);
            if (!identifiers.Add(rule.Id))
                throw new ArgumentException($"A validation rule with identifier '{rule.Id}' is already registered.",
                    nameof(rules));
        }
    }
}
