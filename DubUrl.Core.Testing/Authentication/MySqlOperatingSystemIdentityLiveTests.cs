using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

public class MySqlOperatingSystemIdentityLiveTests : KerberosLiveContract
{
    [TestCase("MARIADB", "MySqlConnector", false), TestCase("MARIADB", "MySqlConnector", true)]
    [TestCase("MYSQL", "MySql.Data", false), TestCase("MYSQL", "MySql.Data", true)]
    [TestCase("SINGLESTORE", "SingleStoreConnector", false), TestCase("SINGLESTORE", "SingleStoreConnector", true)]
    public Task ExistingTicketAuthenticatesConfiguredDatabaseAccount(string fixture, string provider, bool asynchronous)
    {
        if (provider == "MySql.Data" && OperatingSystem.IsMacOS()) Assert.Ignore("Connector.NET Kerberos is unsupported on macOS.");
        return VerifyExistingTicketAsync(fixture, () => MySqlOperatingSystemIdentityTests.Registry(provider), asynchronous,
            "SELECT CURRENT_USER()", Environment.GetEnvironmentVariable("DUBURL_KERBEROS_REQUIRE_TLS") == "1");
    }

    [TestCase("MARIADB", "MySqlConnector", false), TestCase("MARIADB", "MySqlConnector", true)]
    [TestCase("MYSQL", "MySql.Data", false), TestCase("MYSQL", "MySql.Data", true)]
    [TestCase("SINGLESTORE", "SingleStoreConnector", false), TestCase("SINGLESTORE", "SingleStoreConnector", true)]
    public Task PluginOnlyAccountRejectsOtherOrMissingTicket(string fixture, string provider, bool asynchronous)
        => VerifyRejectedTicketAsync(fixture, () => MySqlOperatingSystemIdentityTests.Registry(provider), asynchronous);
}
