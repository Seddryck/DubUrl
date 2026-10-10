using NUnit.Framework;

namespace DubUrl.Testing;

public class CommonExtensionsTest
{
    [Test]
    public void To_DateTimeToDateOnly_ReturnsSameDate()
    {
        var value = new DateTime(2026, 10, 9, 14, 30, 0);

        var result = value.To(typeof(DateOnly));

        Assert.That(result, Is.EqualTo(new DateOnly(2026, 10, 9)));
    }

    [Test]
    public void To_IsoDateStringToDateOnly_ReturnsDate()
    {
        var result = "2026-10-09".To(typeof(DateOnly));

        Assert.That(result, Is.EqualTo(new DateOnly(2026, 10, 9)));
    }

    [Test]
    public void To_DateOnlyToDateTime_ReturnsMidnight()
    {
        var value = new DateOnly(2026, 10, 9);

        var result = value.To(typeof(DateTime));

        Assert.That(result, Is.EqualTo(new DateTime(2026, 10, 9)));
    }
}
