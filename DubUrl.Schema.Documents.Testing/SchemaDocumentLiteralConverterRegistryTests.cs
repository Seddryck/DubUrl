using System.Data;
using DubUrl.Schema.Constraints;
using DubUrl.Schema.Documents.V1;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentLiteralConverterRegistryTests
{
    private static readonly object[][] DefaultLiteralCases =
    [
        ["ansi-string", "ansi", typeof(string)],
        ["ansi-string-fixed-length", "fixed", typeof(string)],
        ["binary", "AQID", typeof(byte[])],
        ["byte", "255", typeof(byte)],
        ["boolean", "true", typeof(bool)],
        ["currency", "12.50", typeof(decimal)],
        ["date", "2026-10-17", typeof(DateTime)],
        ["datetime", "2026-10-17T13:45:00Z", typeof(DateTime)],
        ["datetime2", "2026-10-17T13:45:00.1234567", typeof(DateTime)],
        ["datetime-offset", "2026-10-17T13:45:00+02:00", typeof(DateTimeOffset)],
        ["decimal", "1234.567", typeof(decimal)],
        ["double", "1.25E+10", typeof(double)],
        ["guid", "6f9619ff-8b86-d011-b42d-00cf4fc964ff", typeof(Guid)],
        ["int16", "-123", typeof(short)],
        ["int32", "-123456", typeof(int)],
        ["int64", "-123456789012", typeof(long)],
        ["object", "opaque", typeof(string)],
        ["sbyte", "-12", typeof(sbyte)],
        ["single", "1.25", typeof(float)],
        ["string", "unicode", typeof(string)],
        ["string-fixed-length", "fixed", typeof(string)],
        ["time", "12:34:56", typeof(TimeSpan)],
        ["uint16", "123", typeof(ushort)],
        ["uint32", "123456", typeof(uint)],
        ["uint64", "123456789012", typeof(ulong)],
        ["varnumeric", "1234.567", typeof(decimal)],
        ["xml", "<value />", typeof(string)]
    ];

    [Test]
    public void CreateDefault_CoversEverySupportedLogicalTypeExactlyOnce()
    {
        var registry = SchemaDocumentLiteralConverterRegistry.CreateDefault();
        var expectedTypes = SchemaDocumentTypes.Supported
            .Select(identifier =>
            {
                SchemaDocumentTypes.TryGetDbType(identifier, out var type);
                return type;
            })
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(registry.Converters.Select(converter => converter.Type), Is.EquivalentTo(expectedTypes));
            Assert.That(registry.Converters.Select(converter => converter.Id), Is.Unique);
            Assert.That(registry.Converters, Has.Count.EqualTo(expectedTypes.Length));
        });
    }

    [TestCaseSource(nameof(DefaultLiteralCases))]
    public void Map_DefaultConverters_PreserveRuntimeValueType(
        string logicalType,
        string representation,
        Type expectedType)
    {
        var document = CreateDocument(logicalType, representation);

        var schema = new SchemaDocumentMapper().Map(document);

        Assert.That(schema.Tables["sample"].Columns["value"].DefaultValue, Is.TypeOf(expectedType));
    }

    [Test]
    public void ReplaceConverter_IsUsedByValidatorAndMapper()
    {
        var registry = SchemaDocumentLiteralConverterRegistry.CreateDefault()
            .ReplaceConverter(new HexInt32Converter());
        var validator = new SchemaDocumentValidator(registry);
        var mapper = new SchemaDocumentMapper(validator);
        var document = CreateDocument("int32", "FF");

        var diagnostics = validator.Validate(document);
        var schema = mapper.Map(document);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(schema.Tables["sample"].Columns["value"].DefaultValue, Is.EqualTo(255));
        });
    }

    [Test]
    public void WithConverter_AfterRemoval_IsUsedByValidatorAndMapper()
    {
        var registry = SchemaDocumentLiteralConverterRegistry.CreateDefault()
            .WithoutConverter(DbType.Int32)
            .WithConverter(new HexInt32Converter());
        var validator = new SchemaDocumentValidator(registry);
        var document = CreateDocument("int32", "10");

        var diagnostics = validator.Validate(document);
        var schema = new SchemaDocumentMapper(validator).Map(document);

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(schema.Tables["sample"].Columns["value"].DefaultValue, Is.EqualTo(16));
        });
    }

    [Test]
    public void Map_NullLiteral_PreservesSuccessfulNullValue()
    {
        var document = CreateDocument("int32", "15");
        document.Tables[0].Checks =
        [
            new CheckDocument
            {
                Kind = "membership",
                Expression = new ExpressionDocument { Kind = "column", Name = "value" },
                Values = [new LiteralDocument { Type = "null" }],
                NullBehavior = "match-null"
            }
        ];

        var constraint = new SchemaDocumentMapper().Map(document).Tables["sample"].Constraints
            .OfType<MembershipCheckConstraint>().Single();

        Assert.That(constraint.Values, Is.EqualTo(new object?[] { null }));
    }

    [Test]
    public void WithoutConverter_ByIdentifier_LeavesOriginalRegistryUnchanged()
    {
        var original = SchemaDocumentLiteralConverterRegistry.CreateDefault();
        var int32Id = original.Converters.Single(converter => converter.Type == DbType.Int32).Id;
        var customized = original.WithoutConverter(int32Id);

        var originalDiagnostics = new SchemaDocumentValidator(original).Validate(CreateDocument("int32", "15"));
        var customizedDiagnostics = new SchemaDocumentValidator(customized).Validate(CreateDocument("int32", "15"));

        Assert.Multiple(() =>
        {
            Assert.That(originalDiagnostics, Is.Empty);
            Assert.That(customized.Converters, Has.None.Matches<ILiteralValueConverter>(converter =>
                converter is not null && converter.Type == DbType.Int32));
            Assert.That(customizedDiagnostics, Has.One.Matches<SchemaDocumentDiagnostic>(diagnostic =>
                diagnostic is not null && diagnostic.Code == SchemaDocumentDiagnosticCodes.InvalidLiteral
                && diagnostic.Message == "Literal type 'int32' is not supported."));
        });
    }

    [Test]
    public void WithConverter_DuplicateIdentifier_Throws()
    {
        var registry = SchemaDocumentLiteralConverterRegistry.CreateDefault();
        var duplicateId = registry.Converters[0].Id;

        Assert.That(
            () => registry.WithConverter(new StubConverter(duplicateId, DbType.DateTimeOffset)),
            Throws.ArgumentException.With.Message.Contains(duplicateId));
    }

    [Test]
    public void WithConverter_AmbiguousTypeOwnership_Throws()
    {
        var registry = SchemaDocumentLiteralConverterRegistry.CreateDefault();

        Assert.That(
            () => registry.WithConverter(new StubConverter("custom.int32", DbType.Int32)),
            Throws.ArgumentException.With.Message.Contains(nameof(DbType.Int32)));
    }

    [TestCase("not-a-number")]
    [TestCase("999999999999999999999999")]
    public void Validate_InvalidOrOverflowingDefault_PreservesStableDiagnostic(string representation)
    {
        var diagnostics = new SchemaDocumentValidator().Validate(CreateDocument("int32", representation));

        Assert.That(diagnostics, Has.One.Matches<SchemaDocumentDiagnostic>(diagnostic =>
            diagnostic is not null && diagnostic.Code == SchemaDocumentDiagnosticCodes.InvalidLiteral
            && diagnostic.Path == "tables[0].columns[0].default"
            && diagnostic.Message == $"Value '{representation}' is not valid for literal type 'int32'."));
    }

    private static SchemaDocument CreateDocument(string logicalType, string representation)
        => new()
        {
            Version = SchemaDocument.SupportedVersion,
            Tables =
            [
                new TableDocument
                {
                    Identity = new DatabaseObjectNameDocument { Name = "sample" },
                    Columns =
                    [
                        new ColumnDocument
                        {
                            Name = "value",
                            Type = logicalType,
                            Default = new LiteralDocument { Type = logicalType, Value = representation }
                        }
                    ]
                }
            ]
        };

    private sealed class HexInt32Converter : ILiteralValueConverter
    {
        public string Id => "custom.hex-int32";
        public DbType Type => DbType.Int32;

        public LiteralConversionResult TryConvert(string value)
            => int.TryParse(value, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var result)
                ? LiteralConversionResult.Success(result)
                : LiteralConversionResult.Failure();
    }

    private sealed class StubConverter(string id, DbType type) : ILiteralValueConverter
    {
        public string Id { get; } = id;
        public DbType Type { get; } = type;
        public LiteralConversionResult TryConvert(string value) => LiteralConversionResult.Success(value);
    }
}
