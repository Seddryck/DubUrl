using DubUrl.Schema.Constraints;
using DubUrl.Schema.Documents.V1;
using RuntimeSchema = DubUrl.Schema.Schema;

namespace DubUrl.Schema.Documents;

public sealed class SchemaDocumentMapper : ISchemaDocumentMapper
{
    private readonly SchemaDocumentValidator validator;
    private readonly SchemaDocumentValueConverter valueConverter;

    public SchemaDocumentMapper()
        : this(new SchemaDocumentValidator())
    { }

    public SchemaDocumentMapper(SchemaDocumentLiteralConverterRegistry literalConverters)
        : this(new SchemaDocumentValidator(literalConverters))
    { }

    public SchemaDocumentMapper(SchemaDocumentValidator validator)
    {
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        valueConverter = validator.ValueConverter;
    }

    public RuntimeSchema Map(ISchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document is not SchemaDocument versionOne)
            throw new SchemaDocumentMappingException(validator.Validate(document));
        return Map(versionOne);
    }

    public RuntimeSchema Map(SchemaDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = validator.Validate(document);
        if (diagnostics.Any(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error))
            throw new SchemaDocumentMappingException(diagnostics);

        try
        {
            var tables = document.Tables.Select(MapTable).ToArray();
            var indexes = document.Indexes.Select(MapIndex).ToArray();
            var views = document.Views.Select(MapView).ToArray();
            return new RuntimeSchema(tables, indexes, views);
        }
        catch (SchemaDocumentMappingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new SchemaDocumentMappingException(
                "The validated schema document contains a construct that could not be mapped.", exception);
        }
    }

    private Table MapTable(TableDocument document)
    {
        var columns = document.Columns.Select(MapColumn).ToArray();
        var columnsByName = columns.ToDictionary(column => column.Name, StringComparer.Ordinal);
        var constraints = new List<Constraint>();
        if (document.PrimaryKey is not null)
            constraints.Add(new PrimaryKeyConstraint(
                document.PrimaryKey.Columns.Select(name => columnsByName[name]).ToArray(),
                document.PrimaryKey.Name));
        constraints.AddRange(document.Checks.Select(MapCheck));
        constraints.AddRange(document.ForeignKeys.Select(foreignKey => new ForeignKeyConstraint(
            foreignKey.Name!,
            foreignKey.SourceColumns,
            MapIdentity(foreignKey.TargetTable),
            foreignKey.TargetColumns)));
        return new Table(MapIdentity(document.Identity), columns, constraints.ToArray(), document.Description);
    }

    private Column MapColumn(ColumnDocument document)
    {
        SchemaDocumentTypes.TryGetDbType(document.Type, out var type);
        var constraints = new List<IConstraint>();
        if (document.Nullable is true)
            constraints.Add(new NullableConstraint());
        else
            constraints.Add(new NotNullableConstraint());
        if (document.Unique is true)
            constraints.Add(new UniquenessConstraint());
        constraints.AddRange(document.Checks.Select(MapCheck));

        var defaultValue = document.Default is null ? null : ConvertLiteral(document.Default);
        var nativeType = document.NativeType is null ? null : new NativeDatabaseType(document.NativeType.Name!);
        var fallback = document.NativeType?.Fallback == "logical-type"
            ? NativeTypeFallback.LogicalType
            : NativeTypeFallback.Error;
        var generation = MapGeneration(document);

        return document switch
        {
            { Length: not null } => new VarLengthColumn(
                document.Name!, type, document.Length.Value, defaultValue, constraints.ToArray(),
                document.Description, nativeType, fallback, generation),
            { Precision: not null } => new NumericColumn(
                document.Name!, type, document.Precision.Value, document.Scale ?? 0, defaultValue,
                constraints.ToArray(), document.Description, nativeType, fallback, generation),
            _ => new Column(
                document.Name!, type, defaultValue, constraints.ToArray(), document.Description,
                nativeType, fallback, generation)
        };
    }

    private ColumnGeneration? MapGeneration(ColumnDocument document)
    {
        if (document.Identity is not null)
            return new IdentityColumnGeneration(document.Identity.Seed, document.Identity.Increment);
        if (document.Generated is not null)
            return new GeneratedColumnGeneration(
                MapExpression(document.Generated.Expression),
                document.Generated.Storage == "virtual"
                    ? GeneratedColumnStorage.Virtual
                    : GeneratedColumnStorage.Stored);
        return null;
    }

    private Constraint MapCheck(CheckDocument document)
        => document.Kind switch
        {
            "comparison" => new CheckConstraint(
                MapExpression(document.Left!), document.Operator!, MapExpression(document.Right!), document.Name),
            "membership" => new MembershipCheckConstraint(
                MapPredicateExpression(document.Expression!),
                document.Values.Select(ConvertLiteral),
                document.NullBehavior == "match-null"
                    ? NullMembershipBehavior.MatchNull
                    : NullMembershipBehavior.SqlThreeValuedLogic,
                document.Name),
            "regex" => new RegexCheckConstraint(
                MapPredicateExpression(document.Expression!),
                document.Pattern!,
                document.NullBehavior == "reject-null"
                    ? RegexNullBehavior.RejectNull
                    : RegexNullBehavior.AllowNull,
                document.Name),
            _ => throw new NotSupportedException($"Check kind '{document.Kind}' is not supported.")
        };

    private static Expression MapPredicateExpression(ExpressionDocument document)
        => document switch
        {
            { Kind: "column" } => new ColumnIdentityExpression(document.Name!),
            { Kind: "function", Arguments.Count: 1 } when document.Arguments[0].Kind == "column"
                => new FunctionColumnIdentityExpression(document.Function!, document.Arguments[0].Name!),
            _ => throw new NotSupportedException(
                $"Expression kind '{document.Kind}' is not supported for membership or regex checks.")
        };

    private Expression MapExpression(ExpressionDocument document)
        => document.Kind switch
        {
            "column" => new ColumnIdentityExpression(document.Name!),
            "value" => new ValueExpression(ConvertLiteral(document.Value!)!),
            "function" => new FunctionExpression(document.Function!, document.Arguments.Select(MapExpression).ToArray()),
            _ => throw new NotSupportedException($"Expression kind '{document.Kind}' is not supported.")
        };

    private object? ConvertLiteral(LiteralDocument document)
    {
        if (!valueConverter.TryConvert(document, out var value, out var error))
            throw new SchemaDocumentMappingException(error!, new InvalidOperationException(error));
        return value;
    }

    private static Index MapIndex(IndexDocument document)
        => new(
            MapIdentity(document.Identity),
            MapIdentity(document.Table),
            document.Columns.Select(column => new IndexColumn(
                column.Name!,
                column.Direction switch
                {
                    "ascending" => IndexSortDirection.Ascending,
                    "descending" => IndexSortDirection.Descending,
                    _ => null
                })).ToArray(),
            document.Unique);

    private View MapView(ViewDocument document)
        => new(
            MapIdentity(document.Identity),
            new ViewQuery(
                MapIdentity(document.Query.Source),
                document.Query.Projections.Select(projection => new ViewProjection(
                    MapExpression(projection.Expression),
                    projection.Alias)).ToArray()));

    private static DatabaseObjectName MapIdentity(DatabaseObjectNameDocument document)
        => new(document.Name!, document.Schema, document.Database, document.Catalog);
}
