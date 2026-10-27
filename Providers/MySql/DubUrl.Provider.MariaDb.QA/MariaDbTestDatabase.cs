namespace DubUrl.Provider.MariaDb.QA;

internal static class MariaDbTestDatabase
{
    public static string ConnectionUrl => Environment.GetEnvironmentVariable("DUBURL_MARIADB_QA_URL")
        ?? throw new InvalidOperationException("Run QA/DubUrl.QA/MariaDb/deploy-mariadb-test-env.ps1 first.");
}
