namespace DubUrl.Providers.PostgreSql.QA;

internal static class PostgreSqlTestDatabase
{
    public static string ConnectionUrl
        => Environment.GetEnvironmentVariable("DUBURL_POSTGRESQL_QA_URL")
            ?? "pgsql://postgres:Password12!@localhost/DubUrl";

    public static string ProviderConnectionString
        => Environment.GetEnvironmentVariable("DUBURL_POSTGRESQL_QA_CONNECTION_STRING")
            ?? "Host=localhost;Database=DubUrl;Username=postgres;Password=Password12!";
}
