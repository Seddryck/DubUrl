using System.Data;
using System.Text.RegularExpressions;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

public sealed partial class SchemaDocumentValidator
{
    private static readonly IReadOnlySet<string> ComparisonOperators =
        new HashSet<string>(["=", "<>", "!=", "<", "<=", ">", ">="], StringComparer.Ordinal);

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
        return diagnostics;
    }

    private static void ValidateTable(
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

    private static void ValidateColumn(
        ColumnDocument column,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        var hasType = SchemaDocumentTypes.TryGetDbType(column.Type, out var type);
        if (string.IsNullOrWhiteSpace(column.Type))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "A logical column type is required.", $"{path}.type"));
        else if (!hasType)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.UnsupportedType,
                $"Logical column type '{column.Type}' is not supported.", $"{path}.type"));

        if (column.Length is <= 0)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                "Column length must be greater than zero.", $"{path}.length"));
        if (column.Length is not null && hasType && !SchemaDocumentTypes.SupportsLength(type))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                $"Logical type '{column.Type}' does not support a length facet.", $"{path}.length"));
        if (column.Precision is 0)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                "Column precision must be greater than zero.", $"{path}.precision"));
        if (column.Scale is not null && column.Precision is null)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                "Column scale requires a precision.", $"{path}.scale"));
        if (column.Scale > column.Precision)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                "Column scale cannot exceed its precision.", $"{path}.scale"));
        if ((column.Precision is not null || column.Scale is not null) && hasType && !SchemaDocumentTypes.SupportsPrecision(type))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                $"Logical type '{column.Type}' does not support precision or scale facets.", $"{path}.precision"));
        if (column.Length is not null && (column.Precision is not null || column.Scale is not null))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                "Length and precision/scale facets are mutually exclusive.", path));

        var generationCount = (column.Identity is null ? 0 : 1) + (column.Generated is null ? 0 : 1);
        if (generationCount > 1)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                "Identity and generated column definitions are mutually exclusive.", path));
        if (column.Default is not null && generationCount > 0)
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                "A generated column cannot also define a default value.", path));
        if (column.Identity is not null)
        {
            if (column.Identity.Increment == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidGeneration,
                    "Identity increment cannot be zero.", $"{path}.identity.increment"));
            if (hasType && !SchemaDocumentTypes.SupportsIdentity(type))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Logical type '{column.Type}' cannot be used for an identity column.", $"{path}.identity"));
        }
        if (column.Generated is not null)
        {
            if (column.Generated.Storage is not ("stored" or "virtual"))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidGeneration,
                    "Generated column storage must be 'stored' or 'virtual'.", $"{path}.generated.storage"));
            ValidateExpression(column.Generated.Expression, $"{path}.generated.expression", columns, diagnostics);
        }

        if (column.Default is not null)
        {
            ValidateLiteral(column.Default, $"{path}.default", diagnostics);
            if (column.Default.Type is not null && column.Default.Type != "null" && column.Type is not null
                && column.Default.Type != column.Type)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Default literal type '{column.Default.Type}' does not match column type '{column.Type}'.",
                    $"{path}.default.type"));
        }

        if (column.NativeType is not null)
            ValidateNativeType(column.NativeType, $"{path}.nativeType", diagnostics);
        ValidateChecks(column.Checks ?? [], $"{path}.checks", columns, diagnostics);
    }

    private static void ValidateNativeType(
        NativeTypeDocument nativeType,
        string path,
        List<SchemaDocumentDiagnostic> diagnostics)
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

    private static void ValidateForeignKeys(
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
            if (string.IsNullOrWhiteSpace(foreignKey.Name))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign-key name is required.", $"{path}.name"));
            if (foreignKey.SourceColumns is null || foreignKey.SourceColumns.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign key must reference at least one source column.", $"{path}.sourceColumns"));
            else
                ValidateColumnReferences(foreignKey.SourceColumns, $"{path}.sourceColumns", sourceColumns, diagnostics, "foreign key");

            var targetKey = ValidateIdentity(foreignKey.TargetTable, $"{path}.targetTable", diagnostics);
            var targetColumns = foreignKey.TargetColumns ?? [];
            if (targetColumns.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign key must reference at least one target column.", $"{path}.targetColumns"));
            if ((foreignKey.SourceColumns?.Count ?? 0) != targetColumns.Count)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidForeignKey,
                    "Foreign-key source and target column counts must match.", path));
            if (targetKey is not null && tables.TryGetValue(targetKey, out var target))
            {
                var targetLookup = (target.Table.Columns ?? [])
                    .Where(column => !string.IsNullOrWhiteSpace(column?.Name))
                    .GroupBy(column => column.Name!, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First()!, StringComparer.Ordinal);
                ValidateColumnReferences(targetColumns, $"{path}.targetColumns", targetLookup, diagnostics, "foreign key");
            }
            else if (targetKey is not null)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                    $"Foreign key references missing table '{FormatIdentity(foreignKey.TargetTable)}'.", $"{path}.targetTable"));
        }
    }

    private static void ValidateIndexes(
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
            var identity = ValidateIdentity(index.Identity, $"{path}.identity", diagnostics);
            if (identity is not null && !identities.Add(identity))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateIndex,
                    $"Index '{FormatIdentity(index.Identity)}' is declared more than once.", $"{path}.identity"));
            var tableIdentity = ValidateIdentity(index.Table, $"{path}.table", diagnostics);
            IReadOnlyDictionary<string, ColumnDocument>? tableColumns = null;
            if (tableIdentity is not null && tables.TryGetValue(tableIdentity, out var table))
                tableColumns = (table.Table.Columns ?? [])
                    .Where(column => !string.IsNullOrWhiteSpace(column?.Name))
                    .GroupBy(column => column.Name!, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First()!, StringComparer.Ordinal);
            else if (tableIdentity is not null)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                    $"Index references missing table '{FormatIdentity(index.Table)}'.", $"{path}.table"));

            var columns = index.Columns ?? [];
            if (columns.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidIndex,
                    "An index must reference at least one column.", $"{path}.columns"));
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var column = columns[columnIndex];
                var columnPath = $"{path}.columns[{columnIndex}]";
                if (column is null || string.IsNullOrWhiteSpace(column.Name))
                {
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                        "An index column name is required.", $"{columnPath}.name"));
                    continue;
                }
                if (!names.Add(column.Name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateReference,
                        $"Index column '{column.Name}' is referenced more than once.", $"{columnPath}.name"));
                if (tableColumns is not null && !tableColumns.ContainsKey(column.Name))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                        $"Index references missing column '{column.Name}'.", $"{columnPath}.name"));
                if (column.Direction is not null && column.Direction is not ("ascending" or "descending"))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidIndex,
                        "Index column direction must be 'ascending' or 'descending'.", $"{columnPath}.direction"));
            }
        }
    }

    private static void ValidateChecks(
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
            switch (check.Kind)
            {
                case "comparison":
                    ValidateExpression(check.Left, $"{checkPath}.left", columns, diagnostics);
                    ValidateExpression(check.Right, $"{checkPath}.right", columns, diagnostics);
                    if (check.Operator is null || !ComparisonOperators.Contains(check.Operator))
                        diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                            "Comparison operator must be one of =, <>, !=, <, <=, >, or >=.", $"{checkPath}.operator"));
                    break;
                case "membership":
                    ValidateExpression(check.Expression, $"{checkPath}.expression", columns, diagnostics);
                    if (check.Values is null || check.Values.Count == 0)
                        diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                            "A membership check requires at least one value.", $"{checkPath}.values"));
                    else
                    {
                        for (var valueIndex = 0; valueIndex < check.Values.Count; valueIndex++)
                            ValidateLiteral(check.Values[valueIndex], $"{checkPath}.values[{valueIndex}]", diagnostics);
                        var types = check.Values.Where(value => value?.Type != "null")
                            .Select(value => value?.Type).Distinct(StringComparer.Ordinal).ToArray();
                        if (types.Length > 1)
                            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                                "Membership values must use the same literal type.", $"{checkPath}.values"));
                        if (check.Values.Any(value => value?.Type == "null") && check.NullBehavior != "match-null")
                            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                                "Membership checks containing null require nullBehavior 'match-null'.", $"{checkPath}.nullBehavior"));
                    }
                    if (check.NullBehavior is not null && check.NullBehavior is not ("sql-three-valued-logic" or "match-null"))
                        diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                            "Membership null behavior must be 'sql-three-valued-logic' or 'match-null'.",
                            $"{checkPath}.nullBehavior"));
                    break;
                case "regex":
                    ValidateExpression(check.Expression, $"{checkPath}.expression", columns, diagnostics);
                    if (check.Pattern is null)
                        diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                            "A regex pattern is required.", $"{checkPath}.pattern"));
                    if (check.NullBehavior is not null && check.NullBehavior is not ("allow-null" or "reject-null"))
                        diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                            "Regex null behavior must be 'allow-null' or 'reject-null'.", $"{checkPath}.nullBehavior"));
                    break;
                default:
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                        "Check kind must be 'comparison', 'membership', or 'regex'.", $"{checkPath}.kind"));
                    break;
            }
        }
    }

    private static void ValidateExpression(
        ExpressionDocument? expression,
        string path,
        IReadOnlyList<ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics)
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
                ValidateLiteral(expression.Value, $"{path}.value", diagnostics);
                break;
            case "function":
                if (string.IsNullOrWhiteSpace(expression.Function))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                        "A function expression requires a function identifier.", $"{path}.function"));
                var arguments = expression.Arguments ?? [];
                for (var index = 0; index < arguments.Count; index++)
                    ValidateExpression(arguments[index], $"{path}.arguments[{index}]", columns, diagnostics);
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
        List<SchemaDocumentDiagnostic> diagnostics)
    {
        if (literal is null)
        {
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                "A typed literal is required.", path));
            return;
        }
        if (!SchemaDocumentValueConverter.TryConvert(literal, out _, out var error))
            diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidLiteral, error!, path));
    }

    private static void ValidateColumnReferences(
        IReadOnlyList<string> references,
        string path,
        IReadOnlyDictionary<string, ColumnDocument> columns,
        List<SchemaDocumentDiagnostic> diagnostics,
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
        List<SchemaDocumentDiagnostic> diagnostics)
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

    private static string FormatIdentity(DatabaseObjectNameDocument identity)
        => string.Join('.', new[] { identity.Catalog, identity.Database, identity.Schema, identity.Name }
            .Where(component => component is not null));

    private static SchemaDocumentDiagnostic Error(string code, string message, string path)
        => new(code, SchemaDocumentDiagnosticSeverity.Error, message, path);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(?: [A-Za-z][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex NativeTypeNamePattern();
}
