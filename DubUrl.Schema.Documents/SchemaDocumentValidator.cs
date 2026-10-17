using System.Data;
using System.Text.RegularExpressions;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

public sealed partial class SchemaDocumentValidator
{
    private static readonly IReadOnlySet<string> ComparisonOperators =
        new HashSet<string>(["=", "<>", "!=", "<", "<=", ">", ">="], StringComparer.Ordinal);
    private readonly SchemaDocumentValidationRuleSet rules;
    internal SchemaDocumentValueConverter ValueConverter { get; }

    public SchemaDocumentValidator()
        : this(SchemaDocumentValidationRuleSet.CreateDefault(),
            SchemaDocumentLiteralConverterRegistry.CreateDefault())
    { }

    public SchemaDocumentValidator(SchemaDocumentValidationRuleSet rules)
        : this(rules, SchemaDocumentLiteralConverterRegistry.CreateDefault())
    { }

    public SchemaDocumentValidator(SchemaDocumentLiteralConverterRegistry literalConverters)
        : this(SchemaDocumentValidationRuleSet.CreateDefault(), literalConverters)
    { }

    public SchemaDocumentValidator(
        SchemaDocumentValidationRuleSet rules,
        SchemaDocumentLiteralConverterRegistry literalConverters)
    {
        this.rules = (rules ?? throw new ArgumentNullException(nameof(rules))).Snapshot();
        ValueConverter = new SchemaDocumentValueConverter(literalConverters);
    }

    public IReadOnlyList<SchemaDocumentDiagnostic> Validate(ISchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not SchemaDocument versionOne)
            return [Error(SchemaDocumentDiagnosticCodes.UnsupportedVersion,
                $"Schema document version {document.Version} is not supported.", "version")];
        return Validate(versionOne);
    }

    public IReadOnlyList<SchemaDocumentDiagnostic> Validate(SchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = new List<SchemaDocumentDiagnostic>();
        if (document.Version != SchemaDocument.SupportedVersion)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.UnsupportedVersion,
                $"Schema document version {document.Version} is not supported.", "version"));

        var tables = document.Tables ?? [];
        var tableLookup = new Dictionary<string, (TableDocument Table, int Index)>(StringComparer.Ordinal);
        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
        {
            var table = tables[tableIndex];
            var path = $"tables[{tableIndex}]";
            var key = ValidateIdentity(table?.Identity, $"{path}.identity", diagnostics);
            if (key is not null && !tableLookup.TryAdd(key, (table!, tableIndex)))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateTable,
                    $"Table '{FormatIdentity(table!.Identity)}' is declared more than once.", $"{path}.identity"));
        }

        for (var tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            if (tables[tableIndex] is { } table)
                ValidateTable(table, tableIndex, tableLookup, diagnostics);
            else
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A table declaration cannot be null.", $"tables[{tableIndex}]"));

        ValidateIndexes(document.Indexes ?? [], tableLookup, diagnostics);
        ValidateViews(document.Views ?? [], tableLookup, diagnostics);
        return diagnostics;
    }

    private void ValidateViews(
        IReadOnlyList<ViewDocument> views,
        IReadOnlyDictionary<string, (TableDocument Table, int Index)> tables,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        var viewLookup = new Dictionary<string, (ViewDocument View, int Index)>(StringComparer.Ordinal);
        for (var viewIndex = 0; viewIndex < views.Count; viewIndex++)
        {
            var view = views[viewIndex];
            var path = $"views[{viewIndex}]";
            if (view is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A view declaration cannot be null.", path));
                continue;
            }

            var key = ValidIdentityKey(view.Identity);
            var isDuplicate = key is not null && !viewLookup.TryAdd(key, (view, viewIndex));
            var conflictsWithTable = key is not null && tables.ContainsKey(key);
            RunRules(new ViewDeclarationValidationContext(view, path, isDuplicate, conflictsWithTable),
                rules.ViewDeclarationRules, diagnostics);
        }

        for (var viewIndex = 0; viewIndex < views.Count; viewIndex++)
        {
            var view = views[viewIndex];
            if (view is null)
                continue;
            var path = $"views[{viewIndex}]";
            var sourceKey = ValidIdentityKey(view.Query?.Source);
            TableDocument? sourceTable = null;
            ViewDocument? sourceView = null;
            IReadOnlyList<ColumnDocument> sourceColumns = [];
            if (sourceKey is not null && tables.TryGetValue(sourceKey, out var table))
            {
                sourceTable = table.Table;
                sourceColumns = table.Table.Columns ?? [];
            }
            else if (sourceKey is not null && viewLookup.TryGetValue(sourceKey, out var resolvedView))
            {
                sourceView = resolvedView.View;
                sourceColumns = GetViewOutputColumns(resolvedView.View);
            }

            RunRules(new ViewValidationContext(view, path, sourceTable, sourceView, sourceColumns, ValueConverter),
                rules.ViewRules, diagnostics);
        }

        RunRules(new ViewGraphValidationContext(
                views.Where(view => view is not null).ToArray()!, viewLookup),
            rules.ViewGraphRules, diagnostics);
    }

    private static IReadOnlyList<ColumnDocument> GetViewOutputColumns(ViewDocument view)
        => (view.Query?.Projections ?? [])
            .Where(projection => projection is not null)
            .Select(projection => projection.Alias ??
                (projection.Expression?.Kind == "column" ? projection.Expression.Name : null))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => new ColumnDocument { Name = name })
            .ToArray();

    private static IEnumerable<string> ExpressionReferences(ExpressionDocument? expression)
    {
        if (expression is null)
            yield break;
        if (expression.Kind == "column" && !string.IsNullOrWhiteSpace(expression.Name))
            yield return expression.Name;
        foreach (var argument in expression.Arguments ?? [])
            foreach (var reference in ExpressionReferences(argument))
                yield return reference;
    }

    private static void ValidateViewCycles(
        IReadOnlyDictionary<string, (ViewDocument View, int Index)> views,
        ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        var completed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (startKey, start) in views)
        {
            if (completed.Contains(startKey))
                continue;
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var path = new List<(string Key, ViewDocument View, int Index)>();
            var currentKey = startKey;
            var current = start;
            while (true)
            {
                if (positions.TryGetValue(currentKey, out var cycleStart))
                {
                    var cycle = path.Skip(cycleStart).Select(item => FormatIdentity(item.View.Identity))
                        .Append(FormatIdentity(current.View.Identity)).ToArray();
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.ViewDependencyCycle,
                        $"View dependency cycle detected: {string.Join(" -> ", cycle)}.",
                        $"views[{current.Index}].query.source"));
                    break;
                }

                positions[currentKey] = path.Count;
                path.Add((currentKey, current.View, current.Index));
                completed.Add(currentKey);
                var sourceKey = IdentityKey(current.View.Query?.Source);
                if (sourceKey is null || !views.TryGetValue(sourceKey, out current))
                    break;
                currentKey = sourceKey;
            }
        }
    }

    private static string? IdentityKey(DatabaseObjectNameDocument? identity)
        => identity is null || string.IsNullOrWhiteSpace(identity.Name)
            ? null
            : string.Join('\u001f', identity.Catalog ?? "<null>", identity.Database ?? "<null>",
                identity.Schema ?? "<null>", identity.Name);

    private void ValidateTable(
        TableDocument table,
        int tableIndex,
        IReadOnlyDictionary<string, (TableDocument Table, int Index)> tables,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        var tablePath = $"tables[{tableIndex}]";
        var columns = table.Columns ?? [];
        var columnLookup = new Dictionary<string, ColumnDocument>(StringComparer.Ordinal);
        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var columnPath = $"{tablePath}.columns[{columnIndex}]";
            var column = columns[columnIndex];
            if (column is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A column declaration cannot be null.", columnPath));
                continue;
            }
            if (string.IsNullOrWhiteSpace(column.Name))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A column name is required.", $"{columnPath}.name"));
            else if (!columnLookup.TryAdd(column.Name, column))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateColumn,
                    $"Column '{column.Name}' is declared more than once.", $"{columnPath}.name"));

            ValidateColumn(column, columnPath, columns, diagnostics);
        }

        ValidatePrimaryKey(table.PrimaryKey, tablePath, columnLookup, diagnostics);
        ValidateChecks(table.Checks ?? [], $"{tablePath}.checks", columns, diagnostics);
        ValidateForeignKeys(table.ForeignKeys ?? [], tablePath, columnLookup, tables, diagnostics);
    }

    private void ValidateColumn(
        ColumnDocument column,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        RunRules(new ColumnValidationContext(column, path, columns, ValueConverter), rules.ColumnRules, diagnostics);
        ValidateChecks(column.Checks ?? [], $"{path}.checks", columns, diagnostics);
    }

    private static void ValidateNativeType(
        NativeTypeDocument nativeType,
        string path,
        ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(nativeType.Name))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "A native type name is required.", $"{path}.name"));
        else if (!NativeTypeNamePattern().IsMatch(nativeType.Name))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidNativeType,
                "Native type names may contain only letters, digits, underscores, and single spaces between identifier parts.",
                $"{path}.name"));
        if (nativeType.Fallback is not ("error" or "logical-type"))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidNativeType,
                "Native type fallback must be 'error' or 'logical-type'.", $"{path}.fallback"));
    }

    private static void ValidatePrimaryKey(
        PrimaryKeyDocument? primaryKey,
        string tablePath,
        IReadOnlyDictionary<string, ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        if (primaryKey is null)
            return;
        var path = $"{tablePath}.primaryKey.columns";
        if (primaryKey.Columns is null || primaryKey.Columns.Count == 0)
        {
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "A primary key must reference at least one column.", path));
            return;
        }
        ValidateColumnReferences(primaryKey.Columns, path, columns, diagnostics, "primary key");
        for (var index = 0; index < primaryKey.Columns.Count; index++)
            if (columns.TryGetValue(primaryKey.Columns[index] ?? string.Empty, out var column) && column.Nullable is true)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Primary-key column '{primaryKey.Columns[index]}' cannot be nullable.", $"{path}[{index}]"));
    }

    private void ValidateForeignKeys(
        IReadOnlyList<ForeignKeyDocument> foreignKeys,
        string tablePath,
        IReadOnlyDictionary<string, ColumnDocument> sourceColumns,
        IReadOnlyDictionary<string, (TableDocument Table, int Index)> tables,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        for (var foreignKeyIndex = 0; foreignKeyIndex < foreignKeys.Count; foreignKeyIndex++)
        {
            var foreignKey = foreignKeys[foreignKeyIndex];
            var path = $"{tablePath}.foreignKeys[{foreignKeyIndex}]";
            if (foreignKey is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign-key declaration cannot be null.", path));
                continue;
            }
            var targetKey = ValidIdentityKey(foreignKey.TargetTable);
            var targetTable = targetKey is not null && tables.TryGetValue(targetKey, out var target)
                ? target.Table
                : null;
            RunRules(new ForeignKeyValidationContext(foreignKey, path, sourceColumns, targetTable),
                rules.ForeignKeyRules, diagnostics);
        }
    }

    private void ValidateIndexes(
        IReadOnlyList<IndexDocument> indexes,
        IReadOnlyDictionary<string, (TableDocument Table, int Index)> tables,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (var indexPosition = 0; indexPosition < indexes.Count; indexPosition++)
        {
            var index = indexes[indexPosition];
            var path = $"indexes[{indexPosition}]";
            if (index is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "An index declaration cannot be null.", path));
                continue;
            }
            var tableKey = ValidIdentityKey(index.Table);
            var table = tableKey is not null && tables.TryGetValue(tableKey, out var target) ? target.Table : null;
            RunRules(new IndexValidationContext(index, path, table, identities), rules.IndexRules, diagnostics);
        }
    }

    private void ValidateChecks(
        IReadOnlyList<CheckDocument> checks,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        for (var checkIndex = 0; checkIndex < checks.Count; checkIndex++)
        {
            var check = checks[checkIndex];
            var checkPath = $"{path}[{checkIndex}]";
            if (check is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A check declaration cannot be null.", checkPath));
                continue;
            }
            RunRules(new CheckValidationContext(check, checkPath, columns, ValueConverter), rules.CheckRules,
                diagnostics);
        }
    }

    private static void ValidateExpression(
        ExpressionDocument? expression,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        ICollection<SchemaDocumentDiagnostic> diagnostics,
        SchemaDocumentValueConverter valueConverter)
    {
        if (expression is null)
        {
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "An expression is required.", path));
            return;
        }
        switch (expression.Kind)
        {
            case "column":
                if (string.IsNullOrWhiteSpace(expression.Name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                        "A column expression requires a name.", $"{path}.name"));
                else if (!columns.Any(column => column?.Name == expression.Name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                        $"Expression references missing column '{expression.Name}'.", $"{path}.name"));
                break;
            case "value":
                ValidateLiteral(expression.Value, $"{path}.value", diagnostics, valueConverter);
                break;
            case "function":
                if (string.IsNullOrWhiteSpace(expression.Function))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                        "A function expression requires a function identifier.", $"{path}.function"));
                var arguments = expression.Arguments ?? [];
                for (var index = 0; index < arguments.Count; index++)
                    ValidateExpression(arguments[index], $"{path}.arguments[{index}]", columns, diagnostics,
                        valueConverter);
                break;
            default:
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidExpression,
                    "Expression kind must be 'column', 'value', or 'function'.", $"{path}.kind"));
                break;
        }
    }

    private static void ValidateLiteral(
        LiteralDocument? literal,
        string path,
        ICollection<SchemaDocumentDiagnostic> diagnostics,
        SchemaDocumentValueConverter valueConverter)
    {
        if (literal is null)
        {
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "A typed literal is required.", path));
            return;
        }
        if (!valueConverter.TryConvert(literal, out _, out var error))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidLiteral, error!, path));
    }

    private static void ValidatePredicateExpression(
        ExpressionDocument? expression,
        string path,
        ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        if (expression is null)
            return;
        var supported = expression.Kind == "column"
                        || expression is { Kind: "function", Arguments.Count: 1 }
                        && expression.Arguments[0].Kind == "column";
        if (!supported)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidExpression,
                "Membership and regex checks require a column or a single-column function expression.", path));
    }

    private static void ValidateColumnReferences(
        IReadOnlyList<string> references,
        string path,
        IReadOnlyDictionary<string, ColumnDocument> columns,
        ICollection<SchemaDocumentDiagnostic> diagnostics,
        string owner)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < references.Count; index++)
        {
            var name = references[index];
            if (string.IsNullOrWhiteSpace(name))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    $"A {owner} column name is required.", $"{path}[{index}]"));
            else
            {
                if (!names.Add(name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateReference,
                        $"Column '{name}' is referenced more than once by the {owner}.", $"{path}[{index}]"));
                if (!columns.ContainsKey(name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                        $"The {owner} references missing column '{name}'.", $"{path}[{index}]"));
            }
        }
    }

    private static string? ValidateIdentity(
        DatabaseObjectNameDocument? identity,
        string path,
        ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Name))
        {
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "An object name is required.", $"{path}.name"));
            return null;
        }
        var invalid = false;
        foreach (var (value, component) in new[]
                 {
                     (identity.Catalog, "catalog"),
                     (identity.Database, "database"),
                     (identity.Schema, "schema")
                 })
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                invalid = true;
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidQualifier,
                    "Qualifier components cannot be empty or whitespace.", $"{path}.{component}"));
            }
        return invalid ? null : string.Join('\u001f',
            identity.Catalog ?? "<null>", identity.Database ?? "<null>", identity.Schema ?? "<null>", identity.Name);
    }

    private static string? ValidIdentityKey(DatabaseObjectNameDocument? identity)
        => identity is null || string.IsNullOrWhiteSpace(identity.Name)
            || new[] { identity.Catalog, identity.Database, identity.Schema }
                .Any(component => component is not null && string.IsNullOrWhiteSpace(component))
                ? null
                : IdentityKey(identity);

    private static IReadOnlyDictionary<string, ColumnDocument> ToColumnLookup(
        IReadOnlyList<ColumnDocument> columns)
        => columns
            .Where(column => !string.IsNullOrWhiteSpace(column?.Name))
            .GroupBy(column => column.Name!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First()!, StringComparer.Ordinal);

    private static void RunRules<TContext>(
        TContext context,
        IReadOnlyList<IValidationRule<TContext>> validationRules,
        ICollection<SchemaDocumentDiagnostic> diagnostics)
    {
        foreach (var rule in validationRules)
            rule.Validate(context, diagnostics);
    }

    private static string FormatIdentity(DatabaseObjectNameDocument identity)
        => string.Join('.', new[] { identity.Catalog, identity.Database, identity.Schema, identity.Name }
            .Where(component => component is not null));

    private static SchemaDocumentDiagnostic Error(string code, string message, string path)
        => new(code, SchemaDocumentDiagnosticSeverity.Error, message, path);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(?: [A-Za-z][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NativeTypeNamePattern();
}
