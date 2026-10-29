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
        if (Environment.GetEnvironmentVariable("DUBURL_KERBEROS_REQUIRE_TLS") == "1")
        {
            command.CommandText = "SHOW STATUS LIKE 'Ssl_cipher'";
            await using var reader = await command.ExecuteReaderAsync(budget.Token);
            Assert.That(await reader.ReadAsync(budget.Token) && !string.IsNullOrEmpty(reader.GetString(1)), Is.True,
                "The isolated fixture requires an encrypted, certificate-verified connection.");
        }
    }

    [TestCase("MARIADB", "MySqlConnector", false), TestCase("MARIADB", "MySqlConnector", true)]
    [TestCase("MYSQL", "MySql.Data", false), TestCase("MYSQL", "MySql.Data", true)]
    [TestCase("SINGLESTORE", "SingleStoreConnector", false), TestCase("SINGLESTORE", "SingleStoreConnector", true)]
    public async Task PluginOnlyAccountRejectsOtherOrMissingTicket(string fixture, string provider, bool asynchronous)
    {
        var scenario = Environment.GetEnvironmentVariable("DUBURL_KERBEROS_NEGATIVE_SCENARIO");
        var url = Environment.GetEnvironmentVariable("DUBURL_" + fixture + "_OS_QA_URL");
        if (scenario is not ("missing" or "wrong") || string.IsNullOrWhiteSpace(url))
            Assert.Ignore("Requires an isolated child process with a missing or different fixture principal's ticket cache.");
        var target = new ConnectionUrl(url!, MySqlOperatingSystemIdentityTests.Registry(provider))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            await using var connection = asynchronous ? await target.OpenAsync(budget.Token) : (DbConnection)target.Open();
        });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        Assert.That(error.InnerException, Is.Null);
    }
}
