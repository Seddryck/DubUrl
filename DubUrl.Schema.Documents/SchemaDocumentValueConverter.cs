using System.Globalization;
using DubUrl.Schema.Documents.V1;

namespace DubUrl.Schema.Documents;

internal static class SchemaDocumentValueConverter
{
    public static bool TryConvert(LiteralDocument literal, out object? value, out string? error)
    {
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

        try
        {
            value = type switch
            {
                System.Data.DbType.AnsiString or System.Data.DbType.AnsiStringFixedLength
                    or System.Data.DbType.String or System.Data.DbType.StringFixedLength
                    or System.Data.DbType.Xml or System.Data.DbType.Object => literal.Value,
                System.Data.DbType.Binary => Convert.FromBase64String(literal.Value),
                System.Data.DbType.Byte => byte.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.Boolean => bool.Parse(literal.Value),
                System.Data.DbType.Currency or System.Data.DbType.Decimal or System.Data.DbType.VarNumeric
                    => decimal.Parse(literal.Value, NumberStyles.Number, CultureInfo.InvariantCulture),
                System.Data.DbType.Date or System.Data.DbType.DateTime or System.Data.DbType.DateTime2
                    => DateTime.Parse(literal.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                System.Data.DbType.DateTimeOffset
                    => DateTimeOffset.Parse(literal.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                System.Data.DbType.Double => double.Parse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                System.Data.DbType.Guid => Guid.Parse(literal.Value),
                System.Data.DbType.Int16 => short.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.Int32 => int.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.Int64 => long.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.SByte => sbyte.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.Single => float.Parse(literal.Value, NumberStyles.Float, CultureInfo.InvariantCulture),
                System.Data.DbType.Time => TimeSpan.Parse(literal.Value, CultureInfo.InvariantCulture),
                System.Data.DbType.UInt16 => ushort.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.UInt32 => uint.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                System.Data.DbType.UInt64 => ulong.Parse(literal.Value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                _ => throw new NotSupportedException($"Literal type '{literal.Type}' is not supported.")
            };
            return true;
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            error = $"Value '{literal.Value}' is not valid for literal type '{literal.Type}'.";
            return false;
        }
    }
}
