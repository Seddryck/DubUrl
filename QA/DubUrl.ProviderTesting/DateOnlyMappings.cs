using System.Data;
using System.Globalization;
using Dapper;

namespace DubUrl.ProviderTesting;

internal static class DateOnlyMappings
{
    private static readonly object SyncRoot = new();
    private static bool registered;

    public static void Register()
    {
        lock (SyncRoot)
        {
            if (registered)
                return;

            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            global::DbReader.ValueConverter.RegisterReadDelegate<DateOnly>(
                (record, ordinal) => Convert(record.GetValue(ordinal)));
            registered = true;
        }
    }

    internal static DateOnly Convert(object value)
    {
        if (value is DateOnly dateOnly)
            return dateOnly;
        if (value is DateTime dateTime)
            return DateOnly.FromDateTime(dateTime);
        if (value is DateTimeOffset dateTimeOffset)
            return DateOnly.FromDateTime(dateTimeOffset.DateTime);
        if (DateOnly.TryParse(System.Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;
        throw new InvalidCastException($"Object of type '{value.GetType()}' cannot be converted to type '{typeof(DateOnly)}'.");
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override DateOnly Parse(object value) => Convert(value);

        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }
    }
}
