using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Providers.DuckDb.QA;

[SetUpFixture]
public sealed class DuckDbTestDatabase
{
    public static string DatabasePath => Path.GetFullPath("Customer.duckdb");
    public static string ConnectionUrl => "duckdb:///Customer.duckdb";

    [OneTimeSetUp]
    public void Initialize()
    {
        if (File.Exists(DatabasePath)) File.Delete(DatabasePath);
        using var connection = new DuckDBConnection($"Data Source={DatabasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Customer (CustomerId INT PRIMARY KEY, FullName VARCHAR(50), BirthDate DATETIME);
            INSERT INTO Customer VALUES
                (1, 'Nikola Tesla', '1856-07-10'), (2, 'Albert Einstein', '1879-03-14'),
                (3, 'John von Neumann', '1903-12-28'), (4, 'Alan Turing', '1912-06-23'),
                (5, 'Linus Torvalds', '1969-12-28');
            """;
        command.ExecuteNonQuery();
    }
}
