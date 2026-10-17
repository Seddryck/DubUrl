using System.Data;
using DubUrl.Schema.Constraints;
using DubUrl.Schema.Documents.V1;
using NUnit.Framework;
using SchemaForeignKeyConstraint = DubUrl.Schema.Constraints.ForeignKeyConstraint;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentMapperTests
{
    private readonly SchemaDocumentMapper mapper = new();

    public static IEnumerable<string> LogicalTypes => SchemaDocumentTypes.Supported;

    [TestCaseSource(nameof(LogicalTypes))]
    public void Map_EveryLogicalType_UsesExpectedDbType(string identifier)
    {
        SchemaDocumentTypes.TryGetDbType(identifier, out var expected);
        var document = CreateSingleColumnDocument(new ColumnDocument { Name = "value", Type = identifier });

        var schema = mapper.Map(document);

        Assert.That(schema.Tables["sample"].Columns["value"].Type, Is.EqualTo(expected));
    }

    [Test]
    public void Map_ColumnShapes_PreservesFacetsAndTypedDefault()
    {
        var document = new SchemaDocument
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "sample" },
                    Columns =
                    [
                        new ColumnDocument { Name = "id", Type = "int32" },
                        new ColumnDocument { Name = "name", Type = "string", Length = 80 },
                        new ColumnDocument
                        {
                            Name = "amount", Type = "decimal", Precision = 12, Scale = 3,
                            Default = new LiteralDocument { Type = "decimal", Value = "19.125" }
                        }
                    ]
                }
            ]
        };

        var table = mapper.Map(document).Tables["sample"];

        Assert.Multiple(() =>
        {
            Assert.That(table.Columns["id"], Is.TypeOf<Column>());
            Assert.That(table.Columns["name"], Is.TypeOf<VarLengthColumn>());
            Assert.That(((VarLengthColumn)table.Columns["name"]).Length, Is.EqualTo(80));
            Assert.That(table.Columns["amount"], Is.TypeOf<NumericColumn>());
            Assert.That(((NumericColumn)table.Columns["amount"]).Precision, Is.EqualTo(12));
            Assert.That(((NumericColumn)table.Columns["amount"]).Scale, Is.EqualTo(3));
            Assert.That(table.Columns["amount"].DefaultValue, Is.EqualTo(19.125m));
        });
    }

    [Test]
    public void Map_CompleteDocument_PreservesOrderIdentitiesConstraintsGenerationAndIndexes()
    {
        var schema = mapper.Map(CreateCompleteDocument());
        var customers = schema.GetTable(new DatabaseObjectName("customers", "sales"));
        var orders = schema.GetTable(new DatabaseObjectName("orders", "sales"));
        var index = schema.GetIndex(new DatabaseObjectName("ux_orders_amount", "sales"));

        Assert.Multiple(() =>
        {
            Assert.That(schema.Tables.Values.Select(table => table.Name), Is.EqualTo(new[] { "customers", "orders" }));
            Assert.That(customers.Description, Is.EqualTo("Customer records"));
            Assert.That(customers.Columns.Keys, Is.EqualTo(new[] { "id", "code" }));
            Assert.That(customers.Columns["id"].Generation, Is.TypeOf<IdentityColumnGeneration>());
            Assert.That(((IdentityColumnGeneration)customers.Columns["id"].Generation!).Seed, Is.EqualTo(100));
            Assert.That(customers.Columns["id"].Constraints, Has.Some.TypeOf<NotNullableConstraint>());
            Assert.That(customers.Columns["code"].Constraints, Has.Some.TypeOf<UniquenessConstraint>());
            Assert.That(customers.Columns["code"].Constraints, Has.Some.TypeOf<RegexCheckConstraint>());
            Assert.That(customers.Columns["code"].NativeType!.Name, Is.EqualTo("TEXT"));
            Assert.That(customers.Columns["code"].NativeTypeFallback, Is.EqualTo(NativeTypeFallback.LogicalType));

            Assert.That(orders.Columns.Keys, Is.EqualTo(new[] { "tenant_id", "id", "amount", "taxed_amount" }));
            Assert.That(orders.Columns["taxed_amount"].Generation, Is.TypeOf<GeneratedColumnGeneration>());
            Assert.That(((GeneratedColumnGeneration)orders.Columns["taxed_amount"].Generation!).Storage,
                Is.EqualTo(GeneratedColumnStorage.Stored));
            Assert.That(orders.Constraints.OfType<PrimaryKeyConstraint>().Single().Name, Is.EqualTo("pk_orders"));
            Assert.That(orders.Constraints.OfType<PrimaryKeyConstraint>().Single().Columns.Keys,
                Is.EqualTo(new[] { "tenant_id", "id" }));
            Assert.That(orders.Constraints.OfType<CheckConstraint>().Single().Name, Is.EqualTo("ck_amount"));
            Assert.That(orders.Constraints.OfType<MembershipCheckConstraint>().Single().Name, Is.EqualTo("ck_tenant"));
            Assert.That(orders.Constraints.OfType<SchemaForeignKeyConstraint>().Single().Name, Is.EqualTo("fk_orders_customer"));

            Assert.That(schema.Indexes.Values.Select(item => item.Name), Is.EqualTo(new[] { "ux_orders_amount" }));
            Assert.That(index.IsUnique, Is.True);
            Assert.That(index.Columns.Keys, Is.EqualTo(new[] { "tenant_id", "amount" }));
            Assert.That(index.Columns["tenant_id"].SortDirection, Is.EqualTo(IndexSortDirection.Ascending));
            Assert.That(index.Columns["amount"].SortDirection, Is.EqualTo(IndexSortDirection.Descending));
        });
    }

    [Test]
    public void Map_CheckKindsAndExpressions_PreservesRuntimeShapes()
    {
        var schema = mapper.Map(CreateCompleteDocument());
        var customers = schema.GetTable(new DatabaseObjectName("customers", "sales"));
        var orders = schema.GetTable(new DatabaseObjectName("orders", "sales"));

        var regex = customers.Columns["code"].Constraints.OfType<RegexCheckConstraint>().Single();
        var comparison = orders.Constraints.OfType<CheckConstraint>().Single();
        var membership = orders.Constraints.OfType<MembershipCheckConstraint>().Single();
        var generated = (GeneratedColumnGeneration)orders.Columns["taxed_amount"].Generation!;

        Assert.Multiple(() =>
        {
            Assert.That(regex.Expression, Is.TypeOf<FunctionColumnIdentityExpression>());
            Assert.That(regex.NullBehavior, Is.EqualTo(RegexNullBehavior.RejectNull));
            Assert.That(comparison.Left, Is.TypeOf<ColumnIdentityExpression>());
            Assert.That(((ValueExpression)comparison.Right).Value, Is.EqualTo(0m));
            Assert.That(membership.Expression, Is.TypeOf<ColumnIdentityExpression>());
            Assert.That(membership.Values, Is.EqualTo(new object[] { 1, 2 }));
            Assert.That(membership.NullBehavior, Is.EqualTo(NullMembershipBehavior.SqlThreeValuedLogic));
            Assert.That(generated.Expression, Is.TypeOf<FunctionExpression>());
            Assert.That(((FunctionExpression)generated.Expression).Expressions,
                Has.Exactly(2).Items);
        });
    }

    [Test]
    public void Map_InvalidDocument_ThrowsAllValidationDiagnostics()
    {
        var document = CreateSingleColumnDocument(new ColumnDocument { Name = "value", Type = "unknown" });
        document.Indexes.Add(new IndexDocument
        {
            Identity = new DatabaseObjectNameDocument { Name = "empty" },
            Table = new DatabaseObjectNameDocument { Name = "missing" }
        });

        var exception = Assert.Throws<SchemaDocumentMappingException>(() => mapper.Map(document));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(
                item => item is not null && item.Code == SchemaDocumentDiagnosticCodes.UnsupportedType));
            Assert.That(exception.Diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(
                item => item is not null && item.Path == "indexes[0].table"));
            Assert.That(exception.Message, Does.Contain("validation error"));
        });
    }

    [Test]
    public void Map_UnsupportedPredicateExpression_IsRejectedDuringValidation()
    {
        var document = CreateSingleColumnDocument(new ColumnDocument { Name = "value", Type = "string" });
        document.Tables[0].Checks.Add(new CheckDocument
        {
            Kind = "regex",
            Expression = new ExpressionDocument
            {
                Kind = "function",
                Function = "concat",
                Arguments =
                [
                    new ExpressionDocument { Kind = "column", Name = "value" },
                    new ExpressionDocument { Kind = "value", Value = new LiteralDocument { Type = "string", Value = "x" } }
                ]
            },
            Pattern = "x"
        });

        var exception = Assert.Throws<SchemaDocumentMappingException>(() => mapper.Map(document));

        Assert.That(exception!.Diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(
            item => item is not null
                    && item.Code == SchemaDocumentDiagnosticCodes.InvalidExpression
                    && item.Path == "tables[0].checks[0].expression"));
    }

    private static SchemaDocument CreateSingleColumnDocument(ColumnDocument column)
        => new()
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "sample" },
                    Columns = [column]
                }
            ]
        };

    private static SchemaDocument CreateCompleteDocument()
        => new()
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "customers" },
                    Description = "Customer records",
                    Columns =
                    [
                        new ColumnDocument
                        {
                            Name = "id", Type = "int32", Nullable = false,
                            Identity = new IdentityColumnDocument { Seed = 100, Increment = 5 }
                        },
                        new ColumnDocument
                        {
                            Name = "code", Type = "string", Length = 40, Nullable = true, Unique = true,
                            NativeType = new NativeTypeDocument { Name = "TEXT", Fallback = "logical-type" },
                            Checks =
                            [
                                new CheckDocument
                                {
                                    Name = "ck_code",
                                    Kind = "regex",
                                    Expression = new ExpressionDocument
                                    {
                                        Kind = "function",
                                        Function = "lower",
                                        Arguments = [new ExpressionDocument { Kind = "column", Name = "code" }]
                                    },
                                    Pattern = "^[a-z]",
                                    NullBehavior = "reject-null"
                                }
                            ]
                        }
                    ],
                    PrimaryKey = new PrimaryKeyDocument { Name = "pk_customers", Columns = ["id"] }
                },
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "orders" },
                    Columns =
                    [
                        new ColumnDocument { Name = "tenant_id", Type = "int32", Nullable = false },
                        new ColumnDocument { Name = "id", Type = "int64", Nullable = false },
                        new ColumnDocument
                        {
                            Name = "amount", Type = "decimal", Precision = 12, Scale = 2,
                            Default = new LiteralDocument { Type = "decimal", Value = "0.00" }
                        },
                        new ColumnDocument
                        {
                            Name = "taxed_amount", Type = "decimal", Precision = 12, Scale = 2,
                            Generated = new GeneratedColumnDocument
                            {
                                Storage = "stored",
                                Expression = new ExpressionDocument
                                {
                                    Kind = "function",
                                    Function = "multiply",
                                    Arguments =
                                    [
                                        new ExpressionDocument { Kind = "column", Name = "amount" },
                                        new ExpressionDocument
                                        {
                                            Kind = "value",
                                            Value = new LiteralDocument { Type = "decimal", Value = "1.21" }
                                        }
                                    ]
                                }
                            }
                        }
                    ],
                    PrimaryKey = new PrimaryKeyDocument { Name = "pk_orders", Columns = ["tenant_id", "id"] },
                    Checks =
                    [
                        new CheckDocument
                        {
                            Name = "ck_amount",
                            Kind = "comparison",
                            Left = new ExpressionDocument { Kind = "column", Name = "amount" },
                            Operator = ">=",
                            Right = new ExpressionDocument
                            {
                                Kind = "value",
                                Value = new LiteralDocument { Type = "decimal", Value = "0.00" }
                            }
                        },
                        new CheckDocument
                        {
                            Name = "ck_tenant",
                            Kind = "membership",
                            Expression = new ExpressionDocument { Kind = "column", Name = "tenant_id" },
                            Values =
                            [
                                new LiteralDocument { Type = "int32", Value = "1" },
                                new LiteralDocument { Type = "int32", Value = "2" }
                            ],
                            NullBehavior = "sql-three-valued-logic"
                        }
                    ],
                    ForeignKeys =
                    [
                        new ForeignKeyDocument
                        {
                            Name = "fk_orders_customer",
                            SourceColumns = ["tenant_id"],
                            TargetTable = new DatabaseObjectNameDocument { Schema = "sales", Name = "customers" },
                            TargetColumns = ["id"]
                        }
                    ]
                }
            ],
            Indexes =
            [
                new IndexDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "ux_orders_amount" },
                    Table = new DatabaseObjectNameDocument { Schema = "sales", Name = "orders" },
                    Unique = true,
                    Columns =
                    [
                        new IndexColumnDocument { Name = "tenant_id", Direction = "ascending" },
                        new IndexColumnDocument { Name = "amount", Direction = "descending" }
                    ]
                }
            ]
        };
}
