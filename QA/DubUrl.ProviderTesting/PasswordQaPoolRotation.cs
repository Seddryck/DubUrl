using System.Data.Common;
using System.Reflection;
using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Changes a real fixture-owned login's password while its old authenticated session is idle in a native pool.</summary>
internal static class PasswordQaPoolRotation
{
    internal static async Task VerifyAsync(PasswordQaDriver driver)
    {
        var account = "duburl_qa_" + Guid.NewGuid().ToString("N")[..16];
        var oldPassword = Guid.NewGuid().ToString("N") + "!Aa1";
        var newPassword = Guid.NewGuid().ToString("N") + "!Aa2";
        using var adminSource = new PasswordQaSource(driver.Credentials);
        await using var admin = await driver.Target(adminSource).OpenAsync();
        var recipe = new AccountRecipe(driver.Family, driver.Uri.Segments.LastOrDefault() ?? "", account);
        await CreateAccountAsync(admin, recipe, oldPassword);
        var pooled = new List<DbConnection>();
        try
        {
            using var source = new PasswordQaSource(new(account, oldPassword));
            var target = driver.Target(source, pooling: true);
            var first = await target.OpenAsync();
            pooled.Add(first);
            var originalSession = await SessionAsync(first, recipe);
            await AssertIdentityAsync(first, recipe, account);
            await first.CloseAsync();

            await ExecuteAdminAsync(admin, recipe.ChangePassword(newPassword));
            await AssertOldPasswordRejectedAsync(driver, account, oldPassword);
            source.Current = new(account, newPassword);
            var second = await target.OpenAsync();
            pooled.Add(second);
            var rotatedSession = await SessionAsync(second, recipe);
            Assert.That(rotatedSession == originalSession, Is.False, "A new credential pool must not return the old authenticated server session.");
            await AssertIdentityAsync(second, recipe, account);
            await second.CloseAsync();

            var third = await target.OpenAsync();
            pooled.Add(third);
            Assert.That(await SessionAsync(third, recipe), Is.EqualTo(rotatedSession), "The new-credential pool must actually reuse its native server session.");
            await AssertIdentityAsync(third, recipe, account);
            Assert.That(source.Calls, Is.EqualTo(3), "Each new ConnectionUrl connection must resolve the rotated source once.");
            Assert.That(source.Disposed, Is.False);
        }
        finally
        {
            try { await CloseOwnedPoolsAsync(pooled); }
            finally { await ExecuteAdminAsync(admin, recipe.DropAccount()); }
        }
    }

    private static async Task CreateAccountAsync(DbConnection admin, AccountRecipe recipe, string password)
    {
        await ExecuteAdminAsync(admin, recipe.CreateLogin(password));
        try
        {
            foreach (var sql in recipe.GrantDatabaseAccess()) await ExecuteAdminAsync(admin, sql);
        }
        catch
        {
            await ExecuteAdminAsync(admin, recipe.DropAccount());
            throw;
        }
    }

    private static async Task AssertOldPasswordRejectedAsync(PasswordQaDriver driver, string account, string oldPassword)
    {
        using var stale = new PasswordQaSource(new(account, oldPassword));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () =>
        {
            await using var connection = await driver.Target(stale).OpenAsync();
        });
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed), "Password rotation must be enforced by the database, independently of cached sessions.");
        PasswordQaDriver.AssertSafe(error, oldPassword);
    }

    private static async Task<string> SessionAsync(DbConnection connection, AccountRecipe recipe)
        => Convert.ToString(await ScalarAsync(connection, recipe.SessionSql), System.Globalization.CultureInfo.InvariantCulture)
            ?? throw new AssertionException("The QA server returned no physical session identity.");

    private static async Task AssertIdentityAsync(DbConnection connection, AccountRecipe recipe, string account)
    {
        var identity = Convert.ToString(await ScalarAsync(connection, recipe.IdentitySql), System.Globalization.CultureInfo.InvariantCulture) ?? "";
        Assert.That(identity.Split('@')[0] == account, Is.True, "The database must report the fixture-owned rotated principal.");
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 10;
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAdminAsync(DbConnection connection, string sql)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 10;
            await command.ExecuteNonQueryAsync();
        }
        catch
        {
            // Generated passwords can occur in DDL; never retain raw provider diagnostics or SQL in test output.
            throw new AssertionException("Fixture-owned authentication account provisioning/cleanup failed; sensitive SQL and provider details are redacted.");
        }
    }

    private static async Task CloseOwnedPoolsAsync(IEnumerable<DbConnection> connections)
    {
        var failed = false;
        foreach (var connection in connections)
        {
            try
            {
                await connection.CloseAsync();
                var clear = connection.GetType().GetMethod("ClearPool", BindingFlags.Public | BindingFlags.Static,
                    null, [connection.GetType()], null) ?? throw new AssertionException("This native driver has no targeted ClearPool API.");
                clear.Invoke(null, [connection]);
            }
            catch { failed = true; }
            finally
            {
                try { await connection.DisposeAsync(); }
                catch { failed = true; }
            }
        }
        Assert.That(failed, Is.False, "Every fixture-owned native connection/pool must be cleaned up; provider details are redacted.");
    }

    private sealed class AccountRecipe(string family, string database, string account)
    {
        private bool SqlServer => family == "sqlserver";
        private bool MySql => family is "mysql" or "mariadb";
        internal string SessionSql => family switch
        { "sqlserver" => "SELECT @@SPID", "mysql" or "mariadb" => "SELECT CONNECTION_ID()", _ => "SELECT pg_backend_pid()" };
        internal string IdentitySql => family switch
        { "sqlserver" => "SELECT SYSTEM_USER", "mysql" or "mariadb" => "SELECT CURRENT_USER()", _ => "SELECT CURRENT_USER" };
        internal string CreateLogin(string password) => family switch
        {
            "sqlserver" => $"CREATE LOGIN [{account}] WITH PASSWORD='{password}', CHECK_POLICY=OFF",
            "mysql" or "mariadb" => $"CREATE USER '{account}'@'%' IDENTIFIED BY '{password}'",
            _ => $"CREATE ROLE \"{account}\" LOGIN PASSWORD '{password}'"
        };
        internal IEnumerable<string> GrantDatabaseAccess()
        {
            if (SqlServer) yield return $"CREATE USER [{account}] FOR LOGIN [{account}]";
            else if (MySql) yield return $"GRANT SELECT ON `{database.Replace("`", "``")}`.* TO '{account}'@'%'";
            else yield return $"GRANT CONNECT ON DATABASE \"{database.Replace("\"", "\"\"")}\" TO \"{account}\"";
        }
        internal string ChangePassword(string password) => family switch
        {
            "sqlserver" => $"ALTER LOGIN [{account}] WITH PASSWORD='{password}'",
            "mysql" or "mariadb" => $"ALTER USER '{account}'@'%' IDENTIFIED BY '{password}'",
            _ => $"ALTER ROLE \"{account}\" PASSWORD '{password}'"
        };
        internal string DropAccount() => family switch
        {
            "sqlserver" => $"DROP USER IF EXISTS [{account}]; DROP LOGIN [{account}]",
            "mysql" or "mariadb" => $"DROP USER IF EXISTS '{account}'@'%'",
            _ => $"REVOKE CONNECT ON DATABASE \"{database.Replace("\"", "\"\"")}\" FROM \"{account}\"; DROP ROLE IF EXISTS \"{account}\""
        };
    }
}
