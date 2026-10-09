using Dapper;
using DuckDB.NET.Native;
using System.Data;
using System.Globalization;

namespace DubUrl.QA;

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
        => value switch
        {
            DateOnly date => date,
            DateTime date => DateOnly.FromDateTime(date),
            DateTimeOffset date => DateOnly.FromDateTime(date.DateTime),
            DuckDBDateOnly date => date,
            string date when DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) => result,
            _ => throw new InvalidCastException(
                $"Object of type '{value.GetType()}' cannot be converted to type '{typeof(DateOnly)}'.")
        };

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
