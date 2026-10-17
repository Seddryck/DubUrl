using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

public sealed partial class SchemaDocumentValidator
{
    internal static SchemaDocumentValidationRuleSet CreateDefaultRuleSet()
        => new(
            [
                new ColumnLogicalTypeRule(),
                new ColumnLengthRule(),
                new ColumnPrecisionScaleRule(),
                new ColumnShapeExclusivityRule(),
                new ColumnGenerationExclusivityRule(),
                new ColumnIdentityRule(),
                new ColumnGeneratedRule(),
                new ColumnDefaultValueRule(),
                new ColumnNativeTypeRule()
            ],
            [
                new ForeignKeyNameRule(),
                new ForeignKeySourceColumnsRule(),
                new ForeignKeyTargetIdentityRule(),
                new ForeignKeyTargetColumnsPresenceRule(),
                new ForeignKeyArityRule(),
                new ForeignKeyTargetColumnsRule()
            ],
            [
                new IndexIdentityRule(),
                new IndexTableRule(),
                new IndexColumnsRule()
            ],
            [
                new CheckKindRule(),
                new ComparisonCheckRule(),
                new MembershipCheckRule(),
                new RegexCheckRule()
            ]);

    private sealed class ColumnLogicalTypeRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.LogicalType;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            if (string.IsNullOrWhiteSpace(column.Type))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A logical column type is required.", $"{context.Path}.type"));
            else if (!SchemaDocumentTypes.TryGetDbType(column.Type, out _))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.UnsupportedType,
                    $"Logical column type '{column.Type}' is not supported.", $"{context.Path}.type"));
        }
    }

    private sealed class ColumnLengthRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.Length;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            var hasType = SchemaDocumentTypes.TryGetDbType(column.Type, out var type);
            if (column.Length is <= 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                    "Column length must be greater than zero.", $"{context.Path}.length"));
            if (column.Length is not null && hasType && !SchemaDocumentTypes.SupportsLength(type))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Logical type '{column.Type}' does not support a length facet.", $"{context.Path}.length"));
        }
    }

    private sealed class ColumnPrecisionScaleRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.PrecisionScale;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            var hasType = SchemaDocumentTypes.TryGetDbType(column.Type, out var type);
            if (column.Precision is 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                    "Column precision must be greater than zero.", $"{context.Path}.precision"));
            if (column.Scale is not null && column.Precision is null)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                    "Column scale requires a precision.", $"{context.Path}.scale"));
            if (column.Scale > column.Precision)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidFacet,
                    "Column scale cannot exceed its precision.", $"{context.Path}.scale"));
            if ((column.Precision is not null || column.Scale is not null)
                && hasType && !SchemaDocumentTypes.SupportsPrecision(type))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Logical type '{column.Type}' does not support precision or scale facets.",
                    $"{context.Path}.precision"));
        }
    }

    private sealed class ColumnShapeExclusivityRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.ShapeExclusivity;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            if (column.Length is not null && (column.Precision is not null || column.Scale is not null))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                    "Length and precision/scale facets are mutually exclusive.", context.Path));
        }
    }

    private sealed class ColumnGenerationExclusivityRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.GenerationExclusivity;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            var generationCount = (column.Identity is null ? 0 : 1) + (column.Generated is null ? 0 : 1);
            if (generationCount > 1)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                    "Identity and generated column definitions are mutually exclusive.", context.Path));
            if (column.Default is not null && generationCount > 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MutuallyExclusive,
                    "A generated column cannot also define a default value.", context.Path));
        }
    }

    private sealed class ColumnIdentityRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.Identity;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            if (column.Identity is not null)
            {
                if (column.Identity.Increment == 0)
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidGeneration,
                        "Identity increment cannot be zero.", $"{context.Path}.identity.increment"));
                if (SchemaDocumentTypes.TryGetDbType(column.Type, out var type)
                    && !SchemaDocumentTypes.SupportsIdentity(type))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                        $"Logical type '{column.Type}' cannot be used for an identity column.",
                        $"{context.Path}.identity"));
            }
        }
    }

    private sealed class ColumnGeneratedRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.Generated;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            if (column.Generated is not null)
            {
                if (column.Generated.Storage is not ("stored" or "virtual"))
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidGeneration,
                        "Generated column storage must be 'stored' or 'virtual'.",
                        $"{context.Path}.generated.storage"));
                ValidateExpression(column.Generated.Expression, $"{context.Path}.generated.expression",
                    context.Columns, diagnostics, context.ValueConverter);
            }
        }
    }

    private sealed class ColumnDefaultValueRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.DefaultValue;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var column = context.Column;
            if (column.Default is null)
                return;
            ValidateLiteral(column.Default, $"{context.Path}.default", diagnostics, context.ValueConverter);
            if (column.Default.Type is not null && column.Default.Type != "null" && column.Type is not null
                && column.Default.Type != column.Type)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                    $"Default literal type '{column.Default.Type}' does not match column type '{column.Type}'.",
                    $"{context.Path}.default.type"));
        }
    }

    private sealed class ColumnNativeTypeRule : IValidationRule<ColumnValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Columns.NativeType;

        public void Validate(ColumnValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Column.NativeType is not null)
                ValidateNativeType(context.Column.NativeType, $"{context.Path}.nativeType", diagnostics);
        }
    }

    private sealed class ForeignKeyNameRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.Name;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(context.ForeignKey.Name))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign-key name is required.", $"{context.Path}.name"));
        }
    }

    private sealed class ForeignKeySourceColumnsRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.SourceColumns;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var columns = context.ForeignKey.SourceColumns;
            if (columns is null || columns.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign key must reference at least one source column.", $"{context.Path}.sourceColumns"));
            else
                ValidateColumnReferences(columns, $"{context.Path}.sourceColumns", context.SourceColumns,
                    diagnostics, "foreign key");
        }
    }

    private sealed class ForeignKeyTargetIdentityRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.TargetTable;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
            => ValidateIdentity(context.ForeignKey.TargetTable, $"{context.Path}.targetTable", diagnostics);
    }

    private sealed class ForeignKeyTargetColumnsPresenceRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.TargetColumnsPresence;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if ((context.ForeignKey.TargetColumns?.Count ?? 0) == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A foreign key must reference at least one target column.", $"{context.Path}.targetColumns"));
        }
    }

    private sealed class ForeignKeyArityRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.Arity;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if ((context.ForeignKey.SourceColumns?.Count ?? 0) != (context.ForeignKey.TargetColumns?.Count ?? 0))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidForeignKey,
                    "Foreign-key source and target column counts must match.", context.Path));
        }
    }

    private sealed class ForeignKeyTargetColumnsRule : IValidationRule<ForeignKeyValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.ForeignKeys.TargetColumns;

        public void Validate(ForeignKeyValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var targetKey = ValidIdentityKey(context.ForeignKey.TargetTable);
            if (targetKey is null)
                return;
            if (context.TargetTable is null)
            {
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                    $"Foreign key references missing table '{FormatIdentity(context.ForeignKey.TargetTable)}'.",
                    $"{context.Path}.targetTable"));
                return;
            }

            var targetLookup = ToColumnLookup(context.TargetTable.Columns ?? []);
            ValidateColumnReferences(context.ForeignKey.TargetColumns ?? [], $"{context.Path}.targetColumns",
                targetLookup, diagnostics, "foreign key");
        }
    }

    private sealed class IndexIdentityRule : IValidationRule<IndexValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Indexes.Identity;

        public void Validate(IndexValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var identity = ValidateIdentity(context.Index.Identity, $"{context.Path}.identity", diagnostics);
            if (identity is not null && !context.DeclaredIdentities.Add(identity))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.DuplicateIndex,
                    $"Index '{FormatIdentity(context.Index.Identity)}' is declared more than once.",
                    $"{context.Path}.identity"));
        }
    }

    private sealed class IndexTableRule : IValidationRule<IndexValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Indexes.Table;

        public void Validate(IndexValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var tableIdentity = ValidateIdentity(context.Index.Table, $"{context.Path}.table", diagnostics);
            if (tableIdentity is not null && context.Table is null)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.MissingReference,
                    $"Index references missing table '{FormatIdentity(context.Index.Table)}'.",
                    $"{context.Path}.table"));
        }
    }

    private sealed class IndexColumnsRule : IValidationRule<IndexValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Indexes.Columns;

        public void Validate(IndexValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var columns = context.Index.Columns ?? [];
            if (columns.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidIndex,
                    "An index must reference at least one column.", $"{context.Path}.columns"));
            var tableColumns = context.Table is null ? null : ToColumnLookup(context.Table.Columns ?? []);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var column = columns[columnIndex];
                var columnPath = $"{context.Path}.columns[{columnIndex}]";
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

    private sealed class CheckKindRule : IValidationRule<CheckValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Checks.Kind;

        public void Validate(CheckValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Check.Kind is not ("comparison" or "membership" or "regex"))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                    "Check kind must be 'comparison', 'membership', or 'regex'.", $"{context.Path}.kind"));
        }
    }

    private sealed class ComparisonCheckRule : IValidationRule<CheckValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Checks.Comparison;

        public void Validate(CheckValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Check.Kind != "comparison")
                return;
            ValidateExpression(context.Check.Left, $"{context.Path}.left", context.Columns, diagnostics,
                context.ValueConverter);
            ValidateExpression(context.Check.Right, $"{context.Path}.right", context.Columns, diagnostics,
                context.ValueConverter);
            if (context.Check.Operator is null || !ComparisonOperators.Contains(context.Check.Operator))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                    "Comparison operator must be one of =, <>, !=, <, <=, >, or >=.",
                    $"{context.Path}.operator"));
        }
    }

    private sealed class MembershipCheckRule : IValidationRule<CheckValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Checks.Membership;

        public void Validate(CheckValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var check = context.Check;
            if (check.Kind != "membership")
                return;
            ValidateExpression(check.Expression, $"{context.Path}.expression", context.Columns, diagnostics,
                context.ValueConverter);
            ValidatePredicateExpression(check.Expression, $"{context.Path}.expression", diagnostics);
            if (check.Values is null || check.Values.Count == 0)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                    "A membership check requires at least one value.", $"{context.Path}.values"));
            else
            {
                for (var valueIndex = 0; valueIndex < check.Values.Count; valueIndex++)
                    ValidateLiteral(check.Values[valueIndex], $"{context.Path}.values[{valueIndex}]", diagnostics,
                        context.ValueConverter);
                var types = check.Values.Where(value => value?.Type != "null")
                    .Select(value => value?.Type).Distinct(StringComparer.Ordinal).ToArray();
                if (types.Length > 1)
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                        "Membership values must use the same literal type.", $"{context.Path}.values"));
                if (check.Values.Any(value => value?.Type == "null") && check.NullBehavior != "match-null")
                    diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                        "Membership checks containing null require nullBehavior 'match-null'.",
                        $"{context.Path}.nullBehavior"));
            }
            if (check.NullBehavior is not null
                && check.NullBehavior is not ("sql-three-valued-logic" or "match-null"))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                    "Membership null behavior must be 'sql-three-valued-logic' or 'match-null'.",
                    $"{context.Path}.nullBehavior"));
        }
    }

    private sealed class RegexCheckRule : IValidationRule<CheckValidationContext>
    {
        public string Id => SchemaDocumentValidationRuleIds.Checks.Regex;

        public void Validate(CheckValidationContext context, ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            var check = context.Check;
            if (check.Kind != "regex")
                return;
            ValidateExpression(check.Expression, $"{context.Path}.expression", context.Columns, diagnostics,
                context.ValueConverter);
            ValidatePredicateExpression(check.Expression, $"{context.Path}.expression", diagnostics);
            if (check.Pattern is null)
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.RequiredValue,
                    "A regex pattern is required.", $"{context.Path}.pattern"));
            if (check.NullBehavior is not null && check.NullBehavior is not ("allow-null" or "reject-null"))
                diagnostics.Add(Error(SchemaDocumentDiagnosticCodes.InvalidCheck,
                    "Regex null behavior must be 'allow-null' or 'reject-null'.",
                    $"{context.Path}.nullBehavior"));
        }
    }
}
