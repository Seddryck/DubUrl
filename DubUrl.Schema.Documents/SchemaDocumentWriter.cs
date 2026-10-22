using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DubUrl.Schema.Documents.V1;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DubUrl.Schema.Documents;

/// <summary>Writes deterministic v1 JSON or YAML, leaving the caller's stream open.</summary>
public sealed class SchemaDocumentWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } }
    };
    private static readonly ISerializer Yaml = new SerializerBuilder()
        .WithQuotingNecessaryStrings(true)
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    private static void OmitEmptyCollections(JsonTypeInfo info)
    {
        foreach (var property in info.Properties)
            if (typeof(System.Collections.ICollection).IsAssignableFrom(property.PropertyType))
                property.ShouldSerialize = (_, value) => value is System.Collections.ICollection { Count: > 0 };
    }

    public void Write(Stream stream, ISchemaDocument document, SchemaDocumentFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(document);
        if (!stream.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(stream));
        if (document is not SchemaDocument v1)
            throw new SchemaDocumentMappingException(new SchemaDocumentValidator().Validate(document));
        var diagnostics = new SchemaDocumentValidator().Validate(v1);
        if (diagnostics.Any(item => item.Severity == SchemaDocumentDiagnosticSeverity.Error))
            throw new SchemaDocumentMappingException(diagnostics);
        var text = format switch
        {
            SchemaDocumentFormat.Json => JsonSerializer.Serialize(v1, JsonOptions),
            SchemaDocumentFormat.Yaml => Yaml.Serialize(v1),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        writer.Write(text.Replace("\r\n", "\n", StringComparison.Ordinal));
        writer.Flush();
    }
}
