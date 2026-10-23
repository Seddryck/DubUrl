using DuckDB.NET.Native;
using NUnit.Framework;

namespace DubUrl.QA;

public class DateOnlyMappingsTest
{
    [TestCaseSource(nameof(Values))]
    public void Convert_ProviderDateValue_ReturnsDateOnly(object value)
        => Assert.That(DateOnlyMappings.Convert(value), Is.EqualTo(new DateOnly(2026, 10, 9)));

    private static object[] Values =>
    [
        new DateOnly(2026, 10, 9),
        new DateTime(2026, 10, 9, 14, 30, 0),
        new DateTimeOffset(2026, 10, 9, 14, 30, 0, TimeSpan.FromHours(2)),
        new DuckDBDateOnly(2026, 10, 9),
        "2026-10-09"
    ];
}
