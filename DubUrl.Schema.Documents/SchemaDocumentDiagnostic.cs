namespace DubUrl.Schema.Documents;

public enum SchemaDocumentDiagnosticSeverity
{
    Warning,
    Error
}

public sealed record SchemaDocumentDiagnostic(
    string Code,
    SchemaDocumentDiagnosticSeverity Severity,
    string Message,
    string Path);

public static class SchemaDocumentDiagnosticCodes
{
    public const string UnsupportedVersion = "SDV001";
    public const string RequiredValue = "SDV002";
    public const string InvalidQualifier = "SDV003";
    public const string DuplicateTable = "SDV004";
    public const string DuplicateColumn = "SDV005";
    public const string DuplicateIndex = "SDV006";
    public const string UnsupportedType = "SDV007";
    public const string InvalidFacet = "SDV008";
    public const string IncompatibleFacet = "SDV009";
    public const string MutuallyExclusive = "SDV010";
    public const string InvalidGeneration = "SDV011";
    public const string InvalidLiteral = "SDV012";
    public const string InvalidExpression = "SDV013";
    public const string InvalidCheck = "SDV014";
    public const string DuplicateReference = "SDV015";
    public const string MissingReference = "SDV016";
    public const string InvalidForeignKey = "SDV017";
    public const string InvalidIndex = "SDV018";
    public const string InvalidNativeType = "SDV019";
}
