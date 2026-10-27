using System.Data.Common;
using SingleStoreConnector;

namespace DubUrl.Provider.SingleStore.QA;

internal static class SingleStoreTestDatabase
{
    public static string ConnectionUrl => Environment.GetEnvironmentVariable("DUBURL_SINGLESTORE_QA_URL")
        ?? throw new InvalidOperationException("Run QA/DubUrl.QA/SingleStore/deploy-singlestore-test-env.ps1 to configure the SingleStore test database.");

    public static SingleStoreConnection CreateConnection()
    {
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnectorFactory.Instance);
        return (SingleStoreConnection)new ConnectionUrl(ConnectionUrl).Connect();
    }
}
