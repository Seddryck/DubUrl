using System.Text.Json;
using System.Text.Json.Serialization;
using DubUrl.Schema.Documents.V1;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DubUrl.Schema.Documents;

public sealed class SchemaDocumentReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public ISchemaDocument Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var format = GetFormat(path);
        using var stream = File.OpenRead(path);
        return Read(stream, format, path);
    }

    public ISchemaDocument Read(Stream stream, SchemaDocumentFormat format, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The schema document stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        var content = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(content))
            throw new SchemaDocumentException("The schema document is empty.", sourceName);

        SchemaDocument document = format switch
        {
            SchemaDocumentFormat.Json => ReadJson(content, sourceName),
            SchemaDocumentFormat.Yaml => ReadYaml(content, sourceName),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown schema document format.")
        };

        if (document.Version == 0)
            throw new SchemaDocumentException("The schema document must declare a version.", sourceName);
        if (document.Version != SchemaDocument.SupportedVersion)
            throw new SchemaDocumentException(
                $"Schema document version {document.Version} is not supported; supported versions: {SchemaDocument.SupportedVersion}.",
                sourceName);

        return document;
    }

    public static SchemaDocumentFormat GetFormat(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => SchemaDocumentFormat.Json,
            ".yaml" or ".yml" => SchemaDocumentFormat.Yaml,
            var extension => throw new SchemaDocumentException(
                $"Cannot infer the schema document format from extension '{extension}'. Use .json, .yaml, or .yml.",
                path)
        };

    private static SchemaDocument ReadJson(string content, string? sourceName)
    {
        try
        {
            return JsonSerializer.Deserialize<SchemaDocument>(content, JsonOptions)
                ?? throw new SchemaDocumentException("The JSON schema document is empty.", sourceName);
        }
        catch (JsonException exception)
        {
            throw new SchemaDocumentException(
                "The JSON schema document is malformed.",
                sourceName,
                ToOneBased(exception.LineNumber),
                ToOneBased(exception.BytePositionInLine),
                exception);
        }
    }

    private static SchemaDocument ReadYaml(string content, string? sourceName)
    {
        try
        {
            return YamlDeserializer.Deserialize<SchemaDocument>(content)
                ?? throw new SchemaDocumentException("The YAML schema document is empty.", sourceName);
        }
        catch (YamlException exception)
        {
            throw new SchemaDocumentException(
                "The YAML schema document is malformed.",
                sourceName,
                checked((int)exception.Start.Line),
                checked((int)exception.Start.Column),
                exception);
        }
    }

    private static int? ToOneBased(long? zeroBased)
        => zeroBased is null ? null : checked((int)zeroBased.Value + 1);
}
