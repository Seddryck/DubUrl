namespace DubUrl.Schema.Documents;

public sealed class SchemaDocumentException : Exception
{
    public string? SourceName { get; }
    public int? Line { get; }
    public int? Column { get; }

    public SchemaDocumentException(
        string message,
        string? sourceName = null,
        int? line = null,
        int? column = null,
        Exception? innerException = null)
        : base(FormatMessage(message, sourceName, line, column), innerException)
    {
        SourceName = sourceName;
        Line = line;
        Column = column;
    }

    private static string FormatMessage(string message, string? sourceName, int? line, int? column)
    {
        var location = sourceName is null ? string.Empty : $" in '{sourceName}'";
        if (line is not null)
            location += $" at line {line}, column {column ?? 1}";
        return $"{message}{location}";
    }
}
