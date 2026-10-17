using DubUrl.Schema.Documents.V1;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentValidatorTests
{
    private readonly SchemaDocumentValidator validator = new();

    [Test]
    public void Validate_CompleteDocument_ReturnsNoDiagnosticsAndDoesNotMutateInput()
    {
        var document = CreateValidDocument();

        var first = validator.Validate(document);
        var second = validator.Validate(document);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Empty);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(document.Tables.Select(table => table.Identity.Name), Is.EqualTo(new[] { "customers", "orders" }));
            Assert.That(document.Tables[1].Columns.Select(column => column.Name),
                Is.EqualTo(new[] { "id", "customer_id", "amount", "taxed_amount" }));
            Assert.That(document.Indexes[0].Columns.Select(column => column.Name),
                Is.EqualTo(new[] { "customer_id", "amount" }));
            Assert.That(document.Views.Select(view => view.Identity.Name),
                Is.EqualTo(new[] { "customer_names", "customer_names_copy" }));
        });
    }

    [Test]
    public void Validate_RequiredAndDuplicateNames_AccumulatesPathDiagnostics()
    {
        var document = CreateValidDocument();
        document.Tables.Add(document.Tables[0]);
        document.Tables[1].Columns[0].Name = null;
        document.Tables[1].Columns[1].Name = "amount";
        document.Indexes.Add(document.Indexes[0]);

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateTable, "tables[2].identity");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.RequiredValue, "tables[1].columns[0].name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateColumn, "tables[1].columns[2].name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateIndex, "indexes[1].identity");
        });
    }

    [Test]
    public void Validate_LogicalTypesAndFacets_ReportsEveryInvalidCombination()
    {
        var document = CreateValidDocument();
        var column = document.Tables[1].Columns[2];
        column.Type = "money-ish";
        column.Length = 0;
        column.Precision = 2;
        column.Scale = 3;

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.UnsupportedType, "tables[1].columns[2].type");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidFacet, "tables[1].columns[2].length");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidFacet, "tables[1].columns[2].scale");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MutuallyExclusive, "tables[1].columns[2]");
        });
    }

    [Test]
    public void Validate_TypeIncompatibleFacets_AreReported()
    {
        var document = CreateValidDocument();
        var id = document.Tables[1].Columns[0];
        id.Length = 10;
        id.Precision = 4;
        id.Scale = 0;
        var name = document.Tables[0].Columns[1];
        name.Identity = new IdentityColumnDocument();

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.IncompatibleFacet, "tables[1].columns[0].length");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.IncompatibleFacet, "tables[1].columns[0].precision");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.IncompatibleFacet, "tables[0].columns[1].identity");
        });
    }

    [Test]
    public void Validate_GenerationSettings_ReportsConflictsAndInvalidValues()
    {
        var document = CreateValidDocument();
        var column = document.Tables[1].Columns[2];
        column.Default = new LiteralDocument { Type = "decimal", Value = "1.00" };
        column.Identity = new IdentityColumnDocument { Increment = 0 };
        column.Generated = new GeneratedColumnDocument { Storage = "cached" };

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics.Count(item => item.Code == SchemaDocumentDiagnosticCodes.MutuallyExclusive), Is.EqualTo(2));
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidGeneration,
                "tables[1].columns[2].identity.increment");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidGeneration,
                "tables[1].columns[2].generated.storage");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidExpression,
                "tables[1].columns[2].generated.expression.kind");
        });
    }

    [Test]
    public void Validate_DefaultLiterals_ReportsMalformedAndMismatchedTypes()
    {
        var document = CreateValidDocument();
        document.Tables[1].Columns[2].Default = new LiteralDocument { Type = "int32", Value = "not-a-number" };

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidLiteral, "tables[1].columns[2].default");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                "tables[1].columns[2].default.type");
        });
    }

    [Test]
    public void Validate_PrimaryKey_ReportsDuplicateMissingAndNullableColumns()
    {
        var document = CreateValidDocument();
        document.Tables[1].Columns[0].Nullable = true;
        document.Tables[1].PrimaryKey!.Columns = ["id", "id", "missing"];

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.IncompatibleFacet,
                "tables[1].primaryKey.columns[0]");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateReference,
                "tables[1].primaryKey.columns[1]");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference,
                "tables[1].primaryKey.columns[2]");
        });
    }

    [Test]
    public void Validate_ForeignKey_ReportsMissingReferencesAndArity()
    {
        var document = CreateValidDocument();
        var foreignKey = document.Tables[1].ForeignKeys[0];
        foreignKey.SourceColumns = ["missing", "missing"];
        foreignKey.TargetTable.Name = "unknown";
        foreignKey.TargetColumns = ["id"];

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference,
                "tables[1].foreignKeys[0].sourceColumns[0]");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateReference,
                "tables[1].foreignKeys[0].sourceColumns[1]");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference,
                "tables[1].foreignKeys[0].targetTable");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidForeignKey,
                "tables[1].foreignKeys[0]");
        });
    }

    [Test]
    public void Validate_Index_ReportsMissingTableColumnsDuplicatesAndDirection()
    {
        var document = CreateValidDocument();
        document.Indexes[0].Columns =
        [
            new IndexColumnDocument { Name = "unknown", Direction = "sideways" },
            new IndexColumnDocument { Name = "unknown" }
        ];
        document.Indexes.Add(new IndexDocument
        {
            Identity = new DatabaseObjectNameDocument { Name = "ix_missing" },
            Table = new DatabaseObjectNameDocument { Name = "missing" },
            Columns = []
        });

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference, "indexes[0].columns[0].name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidIndex, "indexes[0].columns[0].direction");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateReference, "indexes[0].columns[1].name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference, "indexes[1].table");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidIndex, "indexes[1].columns");
        });
    }

    [Test]
    public void Validate_ChecksAndExpressions_ReportStructuralErrors()
    {
        var document = CreateValidDocument();
        document.Tables[1].Checks =
        [
            new CheckDocument { Kind = "comparison", Left = new ExpressionDocument { Kind = "column", Name = "missing" }, Operator = "DROP" },
            new CheckDocument
            {
                Kind = "membership",
                Expression = new ExpressionDocument { Kind = "column", Name = "amount" },
                Values = [new LiteralDocument { Type = "null" }]
            },
            new CheckDocument { Kind = "regex", Expression = new ExpressionDocument { Kind = "function" } },
            new CheckDocument { Kind = "custom" }
        ];

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference, "tables[1].checks[0].left.name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.RequiredValue, "tables[1].checks[0].right");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidCheck, "tables[1].checks[0].operator");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidCheck, "tables[1].checks[1].nullBehavior");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.RequiredValue, "tables[1].checks[2].expression.function");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.RequiredValue, "tables[1].checks[2].pattern");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidCheck, "tables[1].checks[3].kind");
        });
    }

    [Test]
    public void Validate_NativeTypeAndIdentityQualifiers_ReportUnsafeValues()
    {
        var document = CreateValidDocument();
        document.Tables[0].Identity.Schema = " ";
        document.Tables[0].Columns[0].NativeType = new NativeTypeDocument
        {
            Name = "INTEGER; DROP TABLE customers",
            Fallback = "guess"
        };

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidQualifier, "tables[0].identity.schema");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidNativeType,
                "tables[0].columns[0].nativeType.name");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidNativeType,
                "tables[0].columns[0].nativeType.fallback");
        });
    }

    [Test]
    public void Validate_Views_ReportsIdentitySourceProjectionAndAliasErrors()
    {
        var document = CreateValidDocument();
        document.Views[0].Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "customers" };
        document.Views[0].Query.Source = new DatabaseObjectNameDocument { Name = "missing" };
        document.Views[0].Query.Projections =
        [
            new ViewProjectionDocument
            {
                Expression = new ExpressionDocument { Kind = "column", Name = "missing" },
                Alias = "value"
            },
            new ViewProjectionDocument
            {
                Expression = new ExpressionDocument { Kind = "column", Name = "missing" },
                Alias = "value"
            }
        ];
        document.Views[1].Query.Projections = [];
        document.Views.Add(document.Views[1]);

        var diagnostics = validator.Validate(document);

        Assert.Multiple(() =>
        {
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateView, "views[0].identity");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.MissingReference, "views[0].query.source");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateReference,
                "views[0].query.projections[1].alias");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.InvalidView, "views[1].query.projections");
            AssertDiagnostic(diagnostics, SchemaDocumentDiagnosticCodes.DuplicateView, "views[2].identity");
        });
    }

    [Test]
    public void Validate_ViewDependencyCycle_ReportsActionablePath()
    {
        var document = CreateValidDocument();
        document.Views[0].Query.Source = document.Views[1].Identity;
        document.Views[1].Query.Source = document.Views[0].Identity;

        var diagnostics = validator.Validate(document);

        Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(diagnostic =>
            diagnostic is not null
            && diagnostic.Code == SchemaDocumentDiagnosticCodes.ViewDependencyCycle
            && diagnostic.Path == "views[0].query.source"
            && diagnostic.Message.Contains(
                "reporting.customer_names -> reporting.customer_names_copy -> reporting.customer_names")));
    }

    private static SchemaDocument CreateValidDocument()
        => new()
        {
            Version = 1,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "customers" },
                    Columns =
                    [
                        new ColumnDocument
                        {
                            Name = "id", Type = "int32", Nullable = false,
                            Identity = new IdentityColumnDocument()
                        },
                        new ColumnDocument
                        {
                            Name = "name", Type = "string", Length = 100, Nullable = false,
                            Checks =
                            [
                                new CheckDocument
                                {
                                    Kind = "regex",
                                    Expression = new ExpressionDocument { Kind = "column", Name = "name" },
                                    Pattern = "^[A-Z]",
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
                        new ColumnDocument { Name = "id", Type = "int64", Nullable = false },
                        new ColumnDocument { Name = "customer_id", Type = "int32", Nullable = false },
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
                    PrimaryKey = new PrimaryKeyDocument { Name = "pk_orders", Columns = ["id"] },
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
                            Name = "ck_customer",
                            Kind = "membership",
                            Expression = new ExpressionDocument { Kind = "column", Name = "customer_id" },
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
                            SourceColumns = ["customer_id"],
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
                    Identity = new DatabaseObjectNameDocument { Schema = "sales", Name = "ux_orders_customer_amount" },
                    Table = new DatabaseObjectNameDocument { Schema = "sales", Name = "orders" },
                    Unique = true,
                    Columns =
                    [
                        new IndexColumnDocument { Name = "customer_id", Direction = "ascending" },
                        new IndexColumnDocument { Name = "amount", Direction = "descending" }
                    ]
                }
            ],
            Views =
            [
                new ViewDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "reporting", Name = "customer_names" },
                    Query = new ViewQueryDocument
                    {
                        Source = new DatabaseObjectNameDocument { Schema = "sales", Name = "customers" },
                        Projections =
                        [
                            new ViewProjectionDocument
                            {
                                Expression = new ExpressionDocument { Kind = "column", Name = "id" }
                            },
                            new ViewProjectionDocument
                            {
                                Expression = new ExpressionDocument { Kind = "column", Name = "name" },
                                Alias = "customer_name"
                            }
                        ]
                    }
                },
                new ViewDocument
                {
                    Identity = new DatabaseObjectNameDocument { Schema = "reporting", Name = "customer_names_copy" },
                    Query = new ViewQueryDocument
                    {
                        Source = new DatabaseObjectNameDocument { Schema = "reporting", Name = "customer_names" },
                        Projections =
                        [
                            new ViewProjectionDocument
                            {
                                Expression = new ExpressionDocument { Kind = "column", Name = "customer_name" }
                            }
                        ]
                    }
                }
            ]
        };

    private static void AssertDiagnostic(
        IEnumerable<SchemaDocumentDiagnostic> diagnostics,
        string code,
        string path)
        => Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(
            diagnostic => diagnostic is not null
                          && diagnostic.Code == code
                          && diagnostic.Path == path
                          && diagnostic.Severity == SchemaDocumentDiagnosticSeverity.Error));
}
