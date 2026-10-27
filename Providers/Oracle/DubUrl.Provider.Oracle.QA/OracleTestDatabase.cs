using System.Data.Common;
using Oracle.ManagedDataAccess.Client;

namespace DubUrl.Provider.Oracle.QA;

internal static class OracleTestDatabase
{
    public static string ConnectionUrl => Environment.GetEnvironmentVariable("DUBURL_ORACLE_QA_URL")
        ?? throw new InvalidOperationException("Run QA/DubUrl.QA/Oracle/deploy-oracle-test-env.ps1 to configure the Oracle test database.");

    public static OracleConnection Open()
    {
        DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess", OracleClientFactory.Instance);
        return (OracleConnection)new ConnectionUrl(ConnectionUrl).Open();
    }
}
