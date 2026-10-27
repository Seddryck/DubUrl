using NUnit.Framework;
using SingleStoreConnector;

namespace DubUrl.Provider.SingleStore.QA;

[TestFixture, Category("SingleStore")]
public sealed class SingleStoreBehaviorTests
{
    [Test]
    public void RoundTripsDecimalDateTimestampUnicodeAndNull()
    {
        using var connection = SingleStoreTestDatabase.CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select cast(@amount as decimal(18,3)), cast(@day as date), cast(@stamp as datetime(6)), @text, @missing";
        var stamp = new DateTime(2023, 6, 10, 17, 52, 12).AddTicks(1234560);
        command.Parameters.AddWithValue("amount", 17.505m);
        command.Parameters.AddWithValue("day", stamp.Date);
        command.Parameters.AddWithValue("stamp", stamp);
        command.Parameters.AddWithValue("text", "O'Brien — café");
        command.Parameters.AddWithValue("missing", DBNull.Value);
        using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetDecimal(0), Is.EqualTo(17.505m));
            Assert.That(reader.GetDateTime(1), Is.EqualTo(stamp.Date));
            Assert.That(reader.GetDateTime(2), Is.EqualTo(stamp));
            Assert.That(reader.GetString(3), Is.EqualTo("O'Brien — café"));
            Assert.That(reader.IsDBNull(4), Is.True);
        });
    }

    [Test]
    public void RejectsInvalidCredentials()
    {
        using var connection = SingleStoreTestDatabase.CreateConnection();
        var settings = new SingleStoreConnectionStringBuilder(connection.ConnectionString) { Password = Guid.NewGuid().ToString("N"), Pooling = false };
        using var invalid = new SingleStoreConnection(settings.ConnectionString);
        var exception = Assert.Throws<SingleStoreException>(() => invalid.Open());
        Assert.That(exception!.Number, Is.EqualTo(1045));
    }
}
