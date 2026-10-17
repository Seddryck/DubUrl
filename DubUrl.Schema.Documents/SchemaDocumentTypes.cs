using System.Data;

namespace DubUrl.Schema.Documents;

public static class SchemaDocumentTypes
{
    private static readonly IReadOnlyDictionary<string, DbType> Types =
        new Dictionary<string, DbType>(StringComparer.Ordinal)
        {
            ["ansi-string"] = DbType.AnsiString,
            ["binary"] = DbType.Binary,
            ["byte"] = DbType.Byte,
            ["boolean"] = DbType.Boolean,
            ["currency"] = DbType.Currency,
            ["date"] = DbType.Date,
            ["datetime"] = DbType.DateTime,
            ["datetime2"] = DbType.DateTime2,
            ["datetime-offset"] = DbType.DateTimeOffset,
            ["decimal"] = DbType.Decimal,
            ["double"] = DbType.Double,
            ["guid"] = DbType.Guid,
            ["int16"] = DbType.Int16,
            ["int32"] = DbType.Int32,
            ["int64"] = DbType.Int64,
            ["object"] = DbType.Object,
            ["sbyte"] = DbType.SByte,
            ["single"] = DbType.Single,
            ["string"] = DbType.String,
            ["time"] = DbType.Time,
            ["uint16"] = DbType.UInt16,
            ["uint32"] = DbType.UInt32,
            ["uint64"] = DbType.UInt64,
            ["varnumeric"] = DbType.VarNumeric,
            ["xml"] = DbType.Xml,
            ["ansi-string-fixed-length"] = DbType.AnsiStringFixedLength,
            ["string-fixed-length"] = DbType.StringFixedLength
        };

    private static readonly IReadOnlySet<DbType> LengthTypes = new HashSet<DbType>
    {
        DbType.AnsiString,
        DbType.Binary,
        DbType.String,
        DbType.AnsiStringFixedLength,
        DbType.StringFixedLength
    };

    private static readonly IReadOnlySet<DbType> PrecisionTypes = new HashSet<DbType>
    {
        DbType.Currency,
        DbType.Decimal,
        DbType.VarNumeric
    };

    private static readonly IReadOnlySet<DbType> IdentityTypes = new HashSet<DbType>
    {
        DbType.Byte,
        DbType.Int16,
        DbType.Int32,
        DbType.Int64,
        DbType.SByte,
        DbType.UInt16,
        DbType.UInt32,
        DbType.UInt64
    };

    public static IReadOnlyCollection<string> Supported { get; } = Types.Keys.ToArray();

    public static bool TryGetDbType(string? identifier, out DbType type)
        => Types.TryGetValue(identifier ?? string.Empty, out type);

    internal static bool SupportsLength(DbType type) => LengthTypes.Contains(type);
    internal static bool SupportsPrecision(DbType type) => PrecisionTypes.Contains(type);
    internal static bool SupportsIdentity(DbType type) => IdentityTypes.Contains(type);
}
