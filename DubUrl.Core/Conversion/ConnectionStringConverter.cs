using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Rewriting;
using System.Data.Common;
using System.Globalization;

namespace DubUrl.Conversion;

/// <summary>Converts connection strings without opening a database connection.</summary>
public sealed class ConnectionStringConverter
{
    private readonly ISchemeRegistry registry;

    public ConnectionStringConverter() : this(SchemeRegistryBuilder.GetDefault()) { }
    public ConnectionStringConverter(ISchemeRegistry registry)
        => this.registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public ConnectionUri Convert(string scheme, string connectionString)
        => TryConvert(scheme, connectionString, out var uri, out var diagnostic)
            ? uri!
            : throw new ConnectionStringConversionException(diagnostic!);

    public bool TryConvert(string scheme, string connectionString, out ConnectionUri? uri)
        => TryConvert(scheme, connectionString, out uri, out _);

    public bool TryConvert(string scheme, string connectionString,
        out ConnectionUri? uri, out ConnectionStringConversionDiagnostic? diagnostic)
    {
        uri = null;
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(connectionString))
            return Fail(ConnectionStringConversionDiagnosticCode.InvalidInput, out diagnostic);

        var phase = ConnectionStringConversionDiagnosticCode.InvalidInput;
        try
        {
            var key = SchemeKey.Parse(scheme);
            phase = ConnectionStringConversionDiagnosticCode.SchemeNotFound;
            var mapper = registry.GetMapper(key);
            if (mapper is not IReverseConnectionMapper { SupportsReverseConversion: true } reverse)
                return Fail(ConnectionStringConversionDiagnosticCode.ReverseMappingNotSupported, out diagnostic);
            phase = ConnectionStringConversionDiagnosticCode.ProviderNotFound;
            var factory = registry.GetProviderFactory(key);
            var source = factory.CreateConnectionStringBuilder();
            if (source is null)
                return Fail(phase, out diagnostic);
            phase = ConnectionStringConversionDiagnosticCode.InvalidInput;
            source.ConnectionString = connectionString;
            phase = ConnectionStringConversionDiagnosticCode.SettingsNotRepresentable;
            var candidate = reverse.Convert(key, source);
            if (!candidate.Scheme.Equals(key))
                throw new NotSupportedException();
            source.ConnectionString = connectionString;

            // Validate the actual serialized URL, including URI normalization and decoding.
            candidate = ConnectionUri.Parse(candidate.ToString(redactPassword: false));
            var output = factory.CreateConnectionStringBuilder() ?? throw new NotSupportedException();
            output.ConnectionString = registry.Resolve(candidate).ConnectionString;
            var differences = Differences(source, output, candidate).ToArray();
            if (differences.Length > 0)
            {
                // Some forward mappings inject defaults; explicit options can preserve the source.
                foreach (var name in differences)
                {
                    if (!source.TryGetValue(name, out var value))
                        throw new NotSupportedException();
                    candidate = candidate.WithOption(name, Text(value));
                }
                candidate = ConnectionUri.Parse(candidate.ToString(redactPassword: false));
                output.Clear();
                output.ConnectionString = registry.Resolve(candidate).ConnectionString;
                if (Differences(source, output, candidate).Any())
                    throw new NotSupportedException();
            }
            uri = candidate;
            return true;
        }
        catch (SchemeNotFoundException) { return Fail(ConnectionStringConversionDiagnosticCode.SchemeNotFound, out diagnostic); }
        catch (ProviderNotFoundException) { return Fail(ConnectionStringConversionDiagnosticCode.ProviderNotFound, out diagnostic); }
        catch (Exception exception) when (exception is ArgumentException or FormatException
            or InvalidOperationException or NotSupportedException or OverflowException or DubUrlException)
        { return Fail(phase, out diagnostic); }
    }

    private static IEnumerable<string> Differences(DbConnectionStringBuilder source,
        DbConnectionStringBuilder output, ConnectionUri candidate)
    {
        var names = source.Keys.Cast<string>().Concat(output.Keys.Cast<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            source.TryGetValue(name, out var expected);
            output.TryGetValue(name, out var actual);
            if (Text(expected) == Text(actual))
                continue;
            // URI hosts are case-insensitive; database names and other values are not.
            if (IsHostKeyword(name) && !string.IsNullOrEmpty(candidate.Host)
                && string.Equals(Text(expected), candidate.Host, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Text(actual), candidate.Host, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return name;
        }
    }

    private static string Text(object? value) => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool IsHostKeyword(string name)
        => name.Replace(" ", "").ToLowerInvariant() is "host" or "server" or "datasource" or "account";

    private static bool Fail(ConnectionStringConversionDiagnosticCode code,
        out ConnectionStringConversionDiagnostic? diagnostic)
    {
        diagnostic = new(code, code switch
        {
            ConnectionStringConversionDiagnosticCode.InvalidInput => "The scheme or connection string is invalid or empty.",
            ConnectionStringConversionDiagnosticCode.SchemeNotFound => "The scheme is not registered.",
            ConnectionStringConversionDiagnosticCode.ProviderNotFound => "The provider or its connection-string builder is unavailable.",
            ConnectionStringConversionDiagnosticCode.ReverseMappingNotSupported => "This mapper does not support reverse conversion.",
            _ => "The connection settings cannot be represented by this scheme without changing their meaning.",
        });
        return false;
    }
}
