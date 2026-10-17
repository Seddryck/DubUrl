using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;

namespace DubUrl.Schema.Documents;

public interface ILiteralValueConverter
{
    string Id { get; }
    DbType Type { get; }
    LiteralConversionResult TryConvert(string value);
}

public readonly record struct LiteralConversionResult(bool IsSuccess, object? Value)
{
    public static LiteralConversionResult Success(object? value) => new(true, value);
    public static LiteralConversionResult Failure() => new(false, null);
}

public sealed class SchemaDocumentLiteralConverterRegistry
{
    private readonly IReadOnlyDictionary<DbType, ILiteralValueConverter> convertersByType;

    public IReadOnlyList<ILiteralValueConverter> Converters { get; }

    private SchemaDocumentLiteralConverterRegistry(IEnumerable<ILiteralValueConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);
        var snapshot = converters.ToArray();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var byType = new Dictionary<DbType, ILiteralValueConverter>();
        foreach (var converter in snapshot)
        {
            ArgumentNullException.ThrowIfNull(converter);
            ArgumentException.ThrowIfNullOrWhiteSpace(converter.Id);
            if (!identifiers.Add(converter.Id))
                throw new ArgumentException(
                    $"A literal converter with identifier '{converter.Id}' is already registered.",
                    nameof(converters));
            if (!byType.TryAdd(converter.Type, converter))
                throw new ArgumentException(
                    $"A literal converter for logical type '{converter.Type}' is already registered.",
                    nameof(converters));
        }

        Converters = new ReadOnlyCollection<ILiteralValueConverter>(snapshot);
        convertersByType = new ReadOnlyDictionary<DbType, ILiteralValueConverter>(byType);
    }

    public static SchemaDocumentLiteralConverterRegistry CreateDefault()
        => new(DefaultLiteralValueConverters.Create());

    public SchemaDocumentLiteralConverterRegistry WithConverter(ILiteralValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        return new SchemaDocumentLiteralConverterRegistry(Converters.Append(converter));
    }

    public SchemaDocumentLiteralConverterRegistry ReplaceConverter(ILiteralValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        if (!convertersByType.TryGetValue(converter.Type, out var existing))
            throw new InvalidOperationException(
                $"No literal converter for logical type '{converter.Type}' is registered.");
        return new SchemaDocumentLiteralConverterRegistry(
            Converters.Select(item => ReferenceEquals(item, existing) ? converter : item));
    }

    public SchemaDocumentLiteralConverterRegistry WithoutConverter(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new SchemaDocumentLiteralConverterRegistry(
            Converters.Where(converter => !StringComparer.Ordinal.Equals(converter.Id, id)));
    }

    public SchemaDocumentLiteralConverterRegistry WithoutConverter(DbType type)
        => new(Converters.Where(converter => converter.Type != type));

    internal SchemaDocumentLiteralConverterRegistry Snapshot()
        => new(Converters);

    internal bool TryGetConverter(DbType type, out ILiteralValueConverter converter)
        => convertersByType.TryGetValue(type, out converter!);
}

internal sealed class DefaultLiteralValueConverter(
    string id,
    DbType type,
    Func<string, object?> convert) : ILiteralValueConverter
{
    public string Id { get; } = id;
    public DbType Type { get; } = type;

    public LiteralConversionResult TryConvert(string value)
    {
        try
        {
            return LiteralConversionResult.Success(convert(value));
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return LiteralConversionResult.Failure();
        }
    }
}

internal static class DefaultLiteralValueConverters
{
    public static IEnumerable<ILiteralValueConverter> Create()
    {
        yield return Create("literal.ansi-string", DbType.AnsiString, value => value);
        yield return Create("literal.ansi-string-fixed-length", DbType.AnsiStringFixedLength, value => value);
        yield return Create("literal.binary", DbType.Binary, Convert.FromBase64String);
        yield return Create("literal.byte", DbType.Byte,
            value => byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.boolean", DbType.Boolean, value => bool.Parse(value));
        yield return Create("literal.currency", DbType.Currency,
            value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture));
        yield return Create("literal.date", DbType.Date,
            value => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        yield return Create("literal.datetime", DbType.DateTime,
            value => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        yield return Create("literal.datetime2", DbType.DateTime2,
            value => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        yield return Create("literal.datetime-offset", DbType.DateTimeOffset,
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        yield return Create("literal.decimal", DbType.Decimal,
            value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture));
        yield return Create("literal.double", DbType.Double,
            value => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
        yield return Create("literal.guid", DbType.Guid, value => Guid.Parse(value));
        yield return Create("literal.int16", DbType.Int16,
            value => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.int32", DbType.Int32,
            value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.int64", DbType.Int64,
            value => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.object", DbType.Object, value => value);
        yield return Create("literal.sbyte", DbType.SByte,
            value => sbyte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.single", DbType.Single,
            value => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
        yield return Create("literal.string", DbType.String, value => value);
        yield return Create("literal.string-fixed-length", DbType.StringFixedLength, value => value);
        yield return Create("literal.time", DbType.Time,
            value => TimeSpan.Parse(value, CultureInfo.InvariantCulture));
        yield return Create("literal.uint16", DbType.UInt16,
            value => ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.uint32", DbType.UInt32,
            value => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.uint64", DbType.UInt64,
            value => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        yield return Create("literal.varnumeric", DbType.VarNumeric,
            value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture));
        yield return Create("literal.xml", DbType.Xml, value => value);
    }

    private static ILiteralValueConverter Create(string id, DbType type, Func<string, object?> convert)
        => new DefaultLiteralValueConverter(id, type, convert);
}
