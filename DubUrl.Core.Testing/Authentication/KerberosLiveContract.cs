using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

// Opt-in fixtures must require Kerberos and restrict which principals can open SQL sessions.
public abstract class KerberosLiveContract
{
    protected static async Task VerifyExistingTicketAsync(string fixture, Func<ISchemeRegistry> registry, bool asynchronous,
        string identitySql, bool verifyMySqlTls = false)
    {
        var prefix = "DUBURL_" + fixture + "_OS_QA_";
        var url = Environment.GetEnvironmentVariable(prefix + "URL");
        var account = Environment.GetEnvironmentVariable(prefix + "ACCOUNT");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(account))
            Assert.Ignore("Missing external Kerberos server/account and existing OS ticket fixture: " + prefix + "URL/ACCOUNT.");
        var target = new ConnectionUrl(url!, registry()).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var connection = asynchronous ? await target.OpenAsync(budget.Token) : (DbConnection)target.Open();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        var result = asynchronous ? await command.ExecuteScalarAsync(budget.Token) : command.ExecuteScalar();
        Assert.That(Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(1));
        command.CommandText = identitySql;
        var actual = asynchronous ? await command.ExecuteScalarAsync(budget.Token) : command.ExecuteScalar();
        Assert.That(string.Equals(actual?.ToString(), account, StringComparison.Ordinal), Is.True,
            "The authenticated database account must match the fixture expectation; identity values are suppressed.");
        if (verifyMySqlTls)
        {
            command.CommandText = "SHOW STATUS LIKE 'Ssl_cipher'";
            await using var reader = await command.ExecuteReaderAsync(budget.Token);
            Assert.That(await reader.ReadAsync(budget.Token) && !string.IsNullOrEmpty(reader.GetString(1)), Is.True,
                "The isolated fixture requires an encrypted, certificate-verified connection.");
        }
    }

    protected static async Task VerifyRejectedTicketAsync(string fixture, Func<ISchemeRegistry> registry, bool asynchronous)
    {
        var scenario = Environment.GetEnvironmentVariable("DUBURL_KERBEROS_NEGATIVE_SCENARIO");
        var url = Environment.GetEnvironmentVariable("DUBURL_" + fixture + "_OS_QA_URL");
        if (scenario is not ("missing" or "wrong") || string.IsNullOrWhiteSpace(url))
            Assert.Ignore("Requires an isolated child process with a missing or different fixture principal's ticket cache.");
        var target = new ConnectionUrl(url!, registry()).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            await using var connection = asynchronous ? await target.OpenAsync(budget.Token) : (DbConnection)target.Open();
        });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        Assert.That(error.InnerException, Is.Null);
    }
}
