using System.Data;
using System.Globalization;
using DubUrl.Adomd.Wrappers;
using Moq;
using NUnit.Framework;

namespace DubUrl.Adomd.Testing;

public class AdomdCommandExtensionsTest
{
    [Test]
    public void ExecuteCellSet_NonAdomdCommand_ThrowsNotSupported()
    {
        var command = Mock.Of<IDbCommand>();

        Assert.That(() => command.ExecuteCellSet(), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void ExecuteCellSet_AdventureWorks_PreservesServerFormattingAndCellOrder()
    {
        var connectionString = Environment.GetEnvironmentVariable("DUBURL_ADOMD_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            Assert.Ignore("Set DUBURL_ADOMD_TEST_CONNECTION to an SSAS connection with the Adventure Works cube.");

        using IDbConnection connection = AdomdFactory.Instance.CreateConnection();
        connection.ConnectionString = connectionString;
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = """
            WITH MEMBER [Measures].[Formatted Example] AS 42, FORMAT_STRING = '"example "0'
            SELECT {[Measures].[Formatted Example], StrToMember(@Measure, CONSTRAINED)} ON COLUMNS,
                   {[Date].[Calendar].DefaultMember} ON ROWS
            FROM [Adventure Works]
            CELL PROPERTIES VALUE, FORMATTED_VALUE
            """;
        var measure = command.CreateParameter();
        measure.ParameterName = "Measure";
        measure.Value = "[Measures].[Internet Sales Amount]";
        command.Parameters.Add(measure);

        var cellSet = command.ExecuteCellSet();

        Assert.That(cellSet.Axes.Count, Is.EqualTo(2));
        Assert.That(cellSet.Axes[0].Set.Tuples.Count, Is.EqualTo(2));
        Assert.That(cellSet.Axes[1].Set.Tuples.Count, Is.EqualTo(1));
        Assert.That(cellSet.Axes[0].Set.Tuples[1].Members[0].UniqueName, Is.EqualTo(measure.Value));
        Assert.That(cellSet.Cells[0].Value, Is.EqualTo(42));
        Assert.That(cellSet.Cells[0, 0].FormattedValue, Is.EqualTo("example 42"));

        var sales = cellSet.Cells[1, 0];
        Assert.That(Convert.ToDecimal(sales.Value, CultureInfo.InvariantCulture), Is.GreaterThan(0m));
        Assert.That(sales.FormattedValue, Is.Not.Empty.And.Not.EqualTo(Convert.ToString(sales.Value, CultureInfo.InvariantCulture)));
    }
}
