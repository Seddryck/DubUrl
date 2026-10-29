using System.Data.Common;
using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

// Opt-in external fixtures must provision accounts exclusively for the documented GSSAPI/
// Kerberos plugin. A matching CURRENT_USER alone cannot prove that server policy.
public class MySqlOperatingSystemIdentityLiveTests
{
    [TestCase("MARIADB", "MySqlConnector", false), TestCase("MARIADB", "MySqlConnector", true)]
    [TestCase("MYSQL", "MySql.Data", false), TestCase("MYSQL", "MySql.Data", true)]
    [TestCase("SINGLESTORE", "SingleStoreConnector", false), TestCase("SINGLESTORE", "SingleStoreConnector", true)]
    public async Task ExistingTicketAuthenticatesConfiguredDatabaseAccount(string fixture, string provider, bool asynchronous)
    {
        var prefix = "DUBURL_" + fixture + "_OS_QA_";
        var url = Environment.GetEnvironmentVariable(prefix + "URL");
        var account = Environment.GetEnvironmentVariable(prefix + "ACCOUNT");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(account))
            Assert.Ignore("Missing external plugin-only server/account and existing OS ticket fixture: " + prefix + "URL/ACCOUNT. No infrastructure is provisioned.");
        if (provider == "MySql.Data" && OperatingSystem.IsMacOS()) Assert.Ignore("Connector.NET Kerberos is unsupported on macOS.");
        var target = new ConnectionUrl(url!, MySqlOperatingSystemIdentityTests.Registry(provider))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = asynchronous ? await target.OpenAsync(budget.Token) : (DbConnection)target.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CURRENT_USER()";
        var actual = asynchronous ? await command.ExecuteScalarAsync(budget.Token) : command.ExecuteScalar();
        Assert.That(string.Equals(actual?.ToString(), account, StringComparison.Ordinal), Is.True,
            "The authenticated database account must match the fixture expectation; identity values are suppressed.");
    }
}
