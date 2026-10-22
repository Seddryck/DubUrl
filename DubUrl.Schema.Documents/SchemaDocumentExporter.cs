using System.Data;
using System.Globalization;
using DubUrl.Schema.Constraints;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

/// <summary>Converts domain schemas to the existing v1 contract, rejecting lossy conversions.</summary>
public sealed class SchemaDocumentExporter
{
    public SchemaDocument Map(Schema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        try
        {
            var document = new SchemaDocument
            {
                Version = 1,
                Tables = schema.Tables.Values.Select(Table).ToList(),
                Indexes = schema.Indexes.Values.Select(index => new IndexDocument
                {
                    Identity = Name(index.Identity),
                    Table = Name(index.TableIdentity),
                    Unique = index.IsUnique,
                    Columns = index.Columns.Values.Select(column => new IndexColumnDocument
                    {
                        Name = column.Name,
                        Direction = column.SortDirection switch
                        {
                            IndexSortDirection.Ascending => "ascending",
                            IndexSortDirection.Descending => "descending",
                            _ => null
                        }
                    }).ToList()
                }).ToList(),
                Views = schema.Views.Values.Select(view => new ViewDocument
                {
                    Identity = Name(view.Identity),
                    Query = new ViewQueryDocument
                    {
                        Source = Name(view.Query.Source),
                        Projections = view.Query.Projections.Select(projection => new ViewProjectionDocument
                        { Expression = Expression(projection.Expression), Alias = projection.Alias }).ToList()
                    }
                }).ToList()
            };
            var diagnostics = new SchemaDocumentValidator().Validate(document);
            if (diagnostics.Any(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error))
                throw new SchemaDocumentMappingException(diagnostics);
            return document;
        }
        catch (SchemaDocumentMappingException) { throw; }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException or OverflowException)
        { throw new SchemaDocumentMappingException("The schema contains a construct that v1 cannot represent without loss.", exception); }
    }

    private static TableDocument Table(Table table)
    {
        var result = new TableDocument
        {
            Identity = Name(table.Identity),
            Description = table.Description,
            Columns = table.Columns.Values.Select(Column).ToList()
        };
        foreach (var constraint in table.Constraints)
            switch (constraint)
            {
                case PrimaryKeyConstraint pk:
                    if (result.PrimaryKey is not null) throw new NotSupportedException();
                    result.PrimaryKey = new() { Name = pk.Name, Columns = pk.Columns.Keys.ToList() }; break;
                case DubUrl.Schema.Constraints.ForeignKeyConstraint fk:
                    result.ForeignKeys.Add(new()
                    {
                        Name = fk.Name,
                        SourceColumns = fk.SourceColumns.ToList(),
                        TargetTable = Name(fk.TargetTable),
                        TargetColumns = fk.TargetColumns.ToList()
                    }); break;
                case CheckConstraint or MembershipCheckConstraint or RegexCheckConstraint:
                    result.Checks.Add(Check(constraint)); break;
                default: throw new NotSupportedException("Unsupported table constraint.");
            }
        return result;
    }
    private static ColumnDocument Column(Column column)
    {
        if (column.GetType() != typeof(Column) && column.GetType() != typeof(VarLengthColumn) && column.GetType() != typeof(NumericColumn))
            throw new NotSupportedException("Custom column types cannot be exported.");
        if (column.NativeType?.Dialects.Count > 0)
            throw new NotSupportedException("v1 does not encode native dialect scopes.");
        if (column.Constraints.OfType<NullableConstraint>().Any() && column.Constraints.OfType<NotNullableConstraint>().Any())
            throw new NotSupportedException("Conflicting nullability constraints cannot be exported.");
        var result = new ColumnDocument
        {
            Name = column.Name,
            Type = TypeName(column.Type),
            Description = column.Description,
            Nullable = !column.Constraints.OfType<NotNullableConstraint>().Any(),
            Default = column.DefaultValue is null ? null : Literal(column.DefaultValue),
            NativeType = column.NativeType is null ? null : new()
            {
                Name = column.NativeType.Name,
                Fallback = column.NativeTypeFallback == NativeTypeFallback.LogicalType ? "logical-type" : "error"
            }
        };
        if (column is VarLengthColumn length) result.Length = length.Length;
        if (column is NumericColumn numeric)
        { result.Precision = checked((byte)numeric.Precision); result.Scale = checked((byte)numeric.Scale); }
        if (column.Generation is IdentityColumnGeneration identity)
            result.Identity = new() { Seed = identity.Seed, Increment = identity.Increment };
        else if (column.Generation is GeneratedColumnGeneration generated)
            result.Generated = new()
            {
                Expression = Expression(generated.Expression),
                Storage = generated.Storage == GeneratedColumnStorage.Stored ? "stored" : "virtual"
            };
        else if (column.Generation is not null) throw new NotSupportedException();
        foreach (var constraint in column.Constraints)
            switch (constraint)
            {
                case NullableConstraint or NotNullableConstraint: break;
                case UniquenessConstraint: result.Unique = true; break;
                case CheckConstraint or MembershipCheckConstraint or RegexCheckConstraint: result.Checks.Add(Check(constraint)); break;
                default: throw new NotSupportedException("Unsupported column constraint.");
            }
        return result;
    }
    private static CheckDocument Check(IConstraint constraint) => constraint switch
    {
        CheckConstraint check => new()
        {
            Name = check.Name,
            Kind = "comparison",
            Left = Expression(check.Left),
            Operator = check.Operator,
            Right = Expression(check.Right)
        },
        MembershipCheckConstraint membership => new()
        {
            Name = membership.Name,
            Kind = "membership",
            Expression = Expression(membership.Expression),
            Values = membership.Values.Select(Literal).ToList(),
            NullBehavior = membership.NullBehavior == NullMembershipBehavior.MatchNull ? "match-null" : "sql-three-valued-logic"
        },
        RegexCheckConstraint regex => new()
        {
            Name = regex.Name,
            Kind = "regex",
            Expression = Expression(regex.Expression),
            Pattern = regex.Pattern,
            NullBehavior = regex.NullBehavior == RegexNullBehavior.RejectNull ? "reject-null" : "allow-null"
        },
        _ => throw new NotSupportedException()
    };
    private static ExpressionDocument Expression(Expression expression) => expression switch
    {
        ColumnIdentityExpression column => new() { Kind = "column", Name = column.Name },
        ColumnExpression column => new() { Kind = "column", Name = column.Column.Name },
        ValueExpression value => new() { Kind = "value", Value = Literal(value.Value) },
        FunctionExpression function => new()
        {
            Kind = "function",
            Function = function.Function,
            Arguments = function.Expressions.Select(Expression).ToList()
        },
        FunctionColumnIdentityExpression function => new()
        {
            Kind = "function",
            Function = function.Function,
            Arguments = [new() { Kind = "column", Name = function.Name }]
        },
        _ => throw new NotSupportedException("Unsupported expression.")
    };
    private static LiteralDocument Literal(object? value) => value switch
    {
        null => new() { Type = "null" },
        byte[] binary => new() { Type = "binary", Value = Convert.ToBase64String(binary) },
        string text => new() { Type = "string", Value = text },
        bool boolean => new() { Type = "boolean", Value = boolean ? "true" : "false" },
        DateTime date => new() { Type = "datetime", Value = date.ToString("O", CultureInfo.InvariantCulture) },
        DateTimeOffset date => new() { Type = "datetime-offset", Value = date.ToString("O", CultureInfo.InvariantCulture) },
        TimeSpan time => new() { Type = "time", Value = time.ToString("c", CultureInfo.InvariantCulture) },
        Guid guid => new() { Type = "guid", Value = guid.ToString("D") },
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => new()
        {
            Type = value switch
            {
                byte => "byte",
                sbyte => "sbyte",
                short => "int16",
                ushort => "uint16",
                int => "int32",
                uint => "uint32",
                long => "int64",
                ulong => "uint64",
                float => "single",
                double => "double",
                _ => "decimal"
            },
            Value = Convert.ToString(value, CultureInfo.InvariantCulture)
        },
        _ => throw new NotSupportedException("Unsupported literal type.")
    };
    private static string TypeName(DbType type) => SchemaDocumentTypes.Supported.Single(name =>
        SchemaDocumentTypes.TryGetDbType(name, out var candidate) && candidate == type);
    private static DatabaseObjectNameDocument Name(DatabaseObjectName name) =>
        new() { Name = name.Name, Schema = name.Schema, Database = name.Database, Catalog = name.Catalog };
}
