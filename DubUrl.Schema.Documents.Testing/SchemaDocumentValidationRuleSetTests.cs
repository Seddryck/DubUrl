using DubUrl.Schema.Documents.V1;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentValidationRuleSetTests
{
    [Test]
    public void CreateDefault_ReturnsCompleteIndependentRuleSets()
    {
        var first = SchemaDocumentValidationRuleSet.CreateDefault();
        var second = SchemaDocumentValidationRuleSet.CreateDefault();
        var customized = first.WithoutColumnRule(SchemaDocumentValidationRuleIds.Columns.LogicalType);

        Assert.Multiple(() =>
        {
            Assert.That(first.ColumnRules, Has.Count.EqualTo(9));
            Assert.That(first.ForeignKeyRules, Has.Count.EqualTo(6));
            Assert.That(first.IndexRules, Has.Count.EqualTo(3));
            Assert.That(first.CheckRules, Has.Count.EqualTo(4));
            Assert.That(customized.ColumnRules, Has.Count.EqualTo(8));
            Assert.That(second.ColumnRules.Select(rule => rule.Id),
                Does.Contain(SchemaDocumentValidationRuleIds.Columns.LogicalType));
        });
    }

    [Test]
    public void Validate_CustomRulesReceivePathsAndResolvedContext()
    {
        var document = CreateDocument();
        var ruleSet = SchemaDocumentValidationRuleSet.CreateDefault()
            .WithColumnRule(new CustomColumnRule())
            .WithForeignKeyRule(new CustomForeignKeyRule())
            .WithIndexRule(new CustomIndexRule())
            .WithCheckRule(new CustomCheckRule());

        var diagnostics = new SchemaDocumentValidator(ruleSet).Validate(document);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Code == "CUSTOM-COLUMN" && item.Path == "tables[1].columns[1]"));
            Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Code == "CUSTOM-FOREIGN-KEY" && item.Path == "tables[1].foreignKeys[0]"));
            Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Code == "CUSTOM-INDEX" && item.Path == "indexes[0]"));
            Assert.That(diagnostics, Has.Some.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Code == "CUSTOM-CHECK" && item.Path == "tables[1].checks[0]"));
        });
    }

    [Test]
    public void Validate_RemovedRulesDoNotProduceTheirDiagnostics()
    {
        var document = CreateDocument();
        document.Tables[0].Columns[0].Type = null;
        document.Tables[1].ForeignKeys[0].Name = null;
        document.Indexes[0].Columns = [];
        document.Tables[1].Checks[0].Kind = "custom";
        var ruleSet = SchemaDocumentValidationRuleSet.CreateDefault()
            .WithoutColumnRule(SchemaDocumentValidationRuleIds.Columns.LogicalType)
            .WithoutForeignKeyRule(SchemaDocumentValidationRuleIds.ForeignKeys.Name)
            .WithoutIndexRule(SchemaDocumentValidationRuleIds.Indexes.Columns)
            .WithoutCheckRule(SchemaDocumentValidationRuleIds.Checks.Kind);

        var diagnostics = new SchemaDocumentValidator(ruleSet).Validate(document);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Has.None.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Path == "tables[0].columns[0].type"));
            Assert.That(diagnostics, Has.None.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Path == "tables[1].foreignKeys[0].name"));
            Assert.That(diagnostics, Has.None.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Path == "indexes[0].columns"));
            Assert.That(diagnostics, Has.None.Matches<SchemaDocumentDiagnostic>(item =>
                item is not null && item.Path == "tables[1].checks[0].kind"));
        });
    }

    [Test]
    public void WithRule_DuplicateIdentifier_Throws()
    {
        var ruleSet = SchemaDocumentValidationRuleSet.CreateDefault();

        Assert.That(
            () => ruleSet.WithColumnRule(new DuplicateColumnRule()),
            Throws.ArgumentException.With.Message.Contains(SchemaDocumentValidationRuleIds.Columns.LogicalType));
    }

    [Test]
    public void Validate_DefaultRulesPreserveDiagnosticOrder()
    {
        var document = CreateDocument();
        var column = document.Tables[0].Columns[0];
        column.Type = null;
        column.Length = 0;
        column.Precision = 2;
        column.Identity = new IdentityColumnDocument { Increment = 0 };
        column.Default = new LiteralDocument { Type = "int32", Value = "invalid" };
        column.NativeType = new NativeTypeDocument { Name = "invalid;type", Fallback = "guess" };

        var diagnostics = new SchemaDocumentValidator().Validate(document)
            .Where(item => item.Path.StartsWith("tables[0].columns[0]", StringComparison.Ordinal))
            .Select(item => (item.Code, item.Path))
            .ToArray();

        Assert.That(diagnostics, Is.EqualTo(new[]
        {
            (SchemaDocumentDiagnosticCodes.RequiredValue, "tables[0].columns[0].type"),
            (SchemaDocumentDiagnosticCodes.InvalidFacet, "tables[0].columns[0].length"),
            (SchemaDocumentDiagnosticCodes.MutuallyExclusive, "tables[0].columns[0]"),
            (SchemaDocumentDiagnosticCodes.MutuallyExclusive, "tables[0].columns[0]"),
            (SchemaDocumentDiagnosticCodes.InvalidGeneration, "tables[0].columns[0].identity.increment"),
            (SchemaDocumentDiagnosticCodes.InvalidLiteral, "tables[0].columns[0].default"),
            (SchemaDocumentDiagnosticCodes.InvalidNativeType, "tables[0].columns[0].nativeType.name"),
            (SchemaDocumentDiagnosticCodes.InvalidNativeType, "tables[0].columns[0].nativeType.fallback")
        }));
    }

    private static SchemaDocument CreateDocument()
        => new()
        {
            Version = SchemaDocument.SupportedVersion,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "parents" },
                    Columns = [new ColumnDocument { Name = "id", Type = "int32" }]
                },
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "children" },
                    Columns =
                    [
                        new ColumnDocument { Name = "id", Type = "int32" },
                        new ColumnDocument { Name = "parent_id", Type = "int32" }
                    ],
                    ForeignKeys =
                    [
                        new ForeignKeyDocument
                        {
                            Name = "fk_children_parents",
                            SourceColumns = ["parent_id"],
                            TargetTable = new DatabaseObjectNameDocument { Name = "parents" },
                            TargetColumns = ["id"]
                        }
                    ],
                    Checks =
                    [
                        new CheckDocument
                        {
                            Kind = "comparison",
                            Left = new ExpressionDocument { Kind = "column", Name = "parent_id" },
                            Operator = ">",
                            Right = new ExpressionDocument
                            {
                                Kind = "value",
                                Value = new LiteralDocument { Type = "int32", Value = "0" }
                            }
                        }
                    ]
                }
            ],
            Indexes =
            [
                new IndexDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "ix_children_parent" },
                    Table = new DatabaseObjectNameDocument { Name = "children" },
                    Columns = [new IndexColumnDocument { Name = "parent_id" }]
                }
            ]
        };

    private abstract class CustomRule<TContext>(string id) : IValidationRule<TContext>
    {
        public string Id { get; } = id;

        public abstract void Validate(TContext context, ICollection<SchemaDocumentDiagnostic> diagnostics);

        protected void AddDiagnostic(ICollection<SchemaDocumentDiagnostic> diagnostics, string code, string path)
            => diagnostics.Add(new SchemaDocumentDiagnostic(
                code, SchemaDocumentDiagnosticSeverity.Warning, "Custom rule executed.", path));
    }

    private sealed class CustomColumnRule() : CustomRule<ColumnValidationContext>("custom.column")
    {
        public override void Validate(
            ColumnValidationContext context,
            ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Column.Name == "parent_id" && context.Columns.Count == 2)
                AddDiagnostic(diagnostics, "CUSTOM-COLUMN", context.Path);
        }
    }

    private sealed class CustomForeignKeyRule() : CustomRule<ForeignKeyValidationContext>("custom.foreign-key")
    {
        public override void Validate(
            ForeignKeyValidationContext context,
            ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.SourceColumns.ContainsKey("parent_id") && context.TargetTable?.Identity.Name == "parents")
                AddDiagnostic(diagnostics, "CUSTOM-FOREIGN-KEY", context.Path);
        }
    }

    private sealed class CustomIndexRule() : CustomRule<IndexValidationContext>("custom.index")
    {
        public override void Validate(
            IndexValidationContext context,
            ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Table?.Identity.Name == "children")
                AddDiagnostic(diagnostics, "CUSTOM-INDEX", context.Path);
        }
    }

    private sealed class CustomCheckRule() : CustomRule<CheckValidationContext>("custom.check")
    {
        public override void Validate(
            CheckValidationContext context,
            ICollection<SchemaDocumentDiagnostic> diagnostics)
        {
            if (context.Columns.Any(column => column.Name == "parent_id"))
                AddDiagnostic(diagnostics, "CUSTOM-CHECK", context.Path);
        }
    }

    private sealed class DuplicateColumnRule()
        : CustomRule<ColumnValidationContext>(SchemaDocumentValidationRuleIds.Columns.LogicalType)
    {
        public override void Validate(
            ColumnValidationContext context,
            ICollection<SchemaDocumentDiagnostic> diagnostics)
        { }
    }
}
