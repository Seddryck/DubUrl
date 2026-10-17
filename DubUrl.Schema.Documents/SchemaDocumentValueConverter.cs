using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

internal sealed class SchemaDocumentValueConverter
{
    private readonly SchemaDocumentLiteralConverterRegistry registry;

    public SchemaDocumentValueConverter(SchemaDocumentLiteralConverterRegistry registry)
        => this.registry = (registry ?? throw new ArgumentNullException(nameof(registry))).Snapshot();

    public bool TryConvert(LiteralDocument literal, out object? value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(literal);
        value = null;
        error = null;
        if (string.IsNullOrWhiteSpace(literal.Type))
        {
            error = "A literal type is required.";
            return false;
        }

        if (literal.Type == "null")
        {
            if (literal.Value is not null)
            {
                error = "A null literal cannot contain a value.";
                return false;
            }
            return true;
        }

        if (!SchemaDocumentTypes.TryGetDbType(literal.Type, out var type))
        {
            error = $"Literal type '{literal.Type}' is not supported.";
            return false;
        }
        if (literal.Value is null)
        {
            error = $"A value is required for literal type '{literal.Type}'.";
            return false;
        }

        if (!registry.TryGetConverter(type, out var converter))
        {
            error = $"Literal type '{literal.Type}' is not supported.";
            return false;
        }

        try
        {
            var result = converter.TryConvert(literal.Value);
            value = result.Value;
            if (result.IsSuccess)
                return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        { }

        value = null;
        error = $"Value '{literal.Value}' is not valid for literal type '{literal.Type}'.";
        return false;
    }
}
