using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Parsing;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal sealed class PasswordQaDriver(string url, ISchemeRegistry registry)
{
    // These fixtures have lifecycle/cancellation recipes; this is not a runtime provider allowlist.
    private static readonly HashSet<string> FixtureRecipes =
    [
        "Microsoft.Data.SqlClient", "Npgsql", "MySqlConnector", "MySql.Data.MySqlClient", "MySql.Data",
        "SingleStoreConnector", "Oracle.ManagedDataAccess", "FirebirdSql.Data.FirebirdClient", "Snowflake.Data",
        "IBM.Data.Db2", "Teradata.Client", "Teradata.Client.Provider"
    ];

    internal ConnectionUri Uri { get; } = ConnectionUri.Parse(url);
    internal ISchemeRegistry Registry { get; } = registry;
    internal string Provider => Registry.GetMapper(Uri.Scheme).GetProviderName();
    internal string Family => Application.ConnectionCheckSql.GetFamily(Uri.Scheme.Canonical);
    internal bool HasFixtureRecipe => FixtureRecipes.Contains(Provider);
    internal UsernamePasswordCredentials Credentials => new(Uri.Username, Uri.Password);
    internal string Location => RemoveUserInfo(url);
    internal string ProbeSql => Application.ConnectionCheckSql.Engine(Family);

    internal void RequireStrategy()
    {
        if (!HasFixtureRecipe)
            Assert.Ignore($"#1689: {Provider} has no verified username/password QA recipe; runtime credentials still use its existing mapper.");
    }

    internal void RequirePasswordAuthentication()
    {
        RequireStrategy();
        if (!Uri.HasUsername || !Uri.HasPassword || Uri.Password.Length == 0)
            Assert.Ignore($"#1689: {Family} QA has no password-protected login. Its insecure/passwordless fixture cannot prove password authentication or rejection.");
    }

    internal void RequirePoolRotation()
    {
        RequirePasswordAuthentication();
        if (Family is not ("sqlserver" or "postgresql" or "timescale" or "mysql" or "mariadb"))
            Assert.Ignore(PoolRotationLimitation());
    }

    private string PoolRotationLimitation() => Family switch
    {
        "firebird" or "firebirdsql" => "#1689: Firebird QA uses a SELECT-only principal; account/password rotation needs fixture-owned security administration, which is not provisioned.",
        "questdb" => "#1689: QuestDB QA provisions one fixed PG-wire login; SQL account/password rotation and a reliable native pool session probe are not provisioned.",
        _ => $"#1689: {Family} QA has no verified account-rotation/session-ID/native-pool cleanup recipe. No fake pool-rotation pass is substituted."
    };

    internal string WithOptions(bool pooling, string? endpoint = null)
    {
        var location = Location;
        if (endpoint is not null)
        {
            var start = location.IndexOf("://", StringComparison.Ordinal) + 3;
            var end = AuthorityEnd(location, start);
            location = location[..start] + endpoint + location[end..];
        }
        var query = location.IndexOf('?');
        if (query >= 0) location = location[..query];
        var options = Uri.Options.Where(option => !option.Key.Equals("Pooling", StringComparison.OrdinalIgnoreCase))
            .Select(option => System.Uri.EscapeDataString(option.Key) + "=" + System.Uri.EscapeDataString(option.Value))
            .ToList();
        // QuestDB deliberately accepts a narrow URL-option set; do not inject unsupported Pooling options.
        if (Family != "questdb") options.Add("Pooling=" + (pooling ? "true" : "false"));
        return options.Count == 0 ? location : location + "?" + string.Join('&', options);
    }

    internal ConnectionUrl Target(PasswordQaSource source, ISchemeRegistry? observer = null, bool pooling = false, string? endpoint = null)
        => new ConnectionUrl(WithOptions(pooling, endpoint), observer ?? Registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(source));

    internal static string RemoveUserInfo(string value)
    {
        var start = value.IndexOf("://", StringComparison.Ordinal) + 3;
        var end = AuthorityEnd(value, start);
        var at = value.LastIndexOf('@', end - 1, end - start);
        return at < start ? value : value[..start] + value[(at + 1)..];
    }

    private static int AuthorityEnd(string value, int start)
    {
        var index = value.IndexOfAny(['/', '?', '#'], start);
        return index < 0 ? value.Length : index;
    }

    internal static void AssertSafe(ConnectionAuthenticationException error, string? password)
    {
        Assert.That(error.InnerException, Is.Null);
        if (string.IsNullOrEmpty(password)) return;
        Assert.That(error.ToString().Contains(password, StringComparison.Ordinal), Is.False, "Authentication errors must not expose credentials.");
        Assert.That(error.ToString().Contains(System.Uri.EscapeDataString(password), StringComparison.Ordinal), Is.False);
    }

    internal static async Task ProbeAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 10;
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(1));
    }
}

internal sealed class PasswordQaSource(UsernamePasswordCredentials credentials) : ISynchronousUsernamePasswordCredentialSource, IDisposable
{
    internal UsernamePasswordCredentials Current { get; set; } = credentials;
    internal bool Fail { get; init; }
    internal bool Block { get; init; }
    internal bool Disposed { get; private set; }
    internal int Calls { get; private set; }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public UsernamePasswordCredentials Resolve(AuthenticationContext context)
    {
        Calls++;
        Entered.TrySetResult();
        if (Fail) throw new InvalidOperationException("qa-sensitive-source-sentinel");
        return Current;
    }

    public async ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
    {
        var result = Resolve(context);
        if (Block) await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public void Dispose() => Disposed = true;
    public override string ToString() => "PasswordQaSource [REDACTED]";
}
