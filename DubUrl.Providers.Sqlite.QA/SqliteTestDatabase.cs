using Microsoft.Data.Sqlite;

namespace DubUrl.Providers.Sqlite.QA;

internal static class SqliteTestDatabase
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "Customer.db");

    public static string ProviderConnectionString => $"Data Source={DatabasePath}";
    public static string ConnectionUrl
    {
        get
        {
            EnsureInitialized();
            return $"sqlite:///{DatabasePath.Replace('\\', '/')}";
        }
    }

    public static void EnsureInitialized()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            SQLitePCL.Batteries_V2.Init();
            using var connection = new SqliteConnection(ProviderConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                drop table if exists Customer;
                create table Customer(CustomerId integer primary key, FullName varchar(50), BirthDate date);
                insert into Customer (FullName, BirthDate) values
                    ('Nikola Tesla', '1856-07-10'),
                    ('Albert Einstein', '1879-03-14'),
                    ('John von Neumann', '1903-12-28'),
                    ('Alan Turing', '1912-06-23'),
                    ('Linus Torvalds', '1969-12-28');
                """;
            command.ExecuteNonQuery();
            _initialized = true;
        }
    }
}
