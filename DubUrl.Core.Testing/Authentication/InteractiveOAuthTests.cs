using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using DubUrl.Authentication.Snowflake;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Snowflake.Data.Client;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class InteractiveOAuthTests
{
    [Test]
    public async Task EntraConfigurationInstallsStableCallbackWithoutAcquisitionAndRejectsSync()
    {
        using var session = new EntraInteractiveOAuthSession();
        var strategy = ConnectionAuthentication.OAuthInteractive(Options(session));
        var target = new ConnectionUrl("mssql://localhost/app", Registry()).WithAuthentication(strategy);
        _ = target.Parse(); _ = target.Dialect;
        using var first = (SqlConnection)await target.ConnectAsync();
        using var second = (SqlConnection)await target.ConnectAsync();
        Assert.That(first.AccessTokenCallback, Is.SameAs(second.AccessTokenCallback));
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("SynchronousCredentialsRequired"));
        var request = new SqlAuthenticationParameters(SqlAuthenticationMethod.ActiveDirectoryInteractive, "localhost", "app", "https://database.windows.net/",
            "https://login.windows.net/tenant/oauth2/authorize", null, null, Guid.NewGuid(), 15);
        var failure = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await first.AccessTokenCallback!(request, CancellationToken.None));
        Assert.That(failure!.Code, Is.EqualTo("InteractionRequired"));
    }

    [Test]
    public async Task SnowflakeConfiguresNativePublicClientPkceWithoutBrowserOrPersistentCache()
    {
        using var session = new SnowflakeInteractiveOAuthSession("fixture");
        var options = new InteractiveOAuthOptions
        {
            ClientId = "LOCAL_APPLICATION", Authority = new("https://fixture.snowflakecomputing.com"), RedirectUri = new("http://127.0.0.1:41589/callback/"),
            Scopes = ["session:role:PUBLIC"], Session = session, AllowInteraction = true
        };
        var target = new ConnectionUrl("snowflake://fixture/app", Registry()).WithAuthentication(ConnectionAuthentication.OAuthInteractive(options));
        if (!Environment.UserInteractive) { Assert.That(async () => await target.ConnectAsync(), Throws.TypeOf<ConnectionAuthenticationException>()); return; }
        using var connection = await target.ConnectAsync();
        var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        Assert.That(builder["AUTHENTICATOR"], Is.EqualTo("oauth_authorization_code"));
        Assert.That(builder["CLIENT_STORE_TEMPORARY_CREDENTIAL"], Is.EqualTo("False"));
        Assert.That(builder["OAUTHREDIRECTURI"], Is.EqualTo(options.RedirectUri.AbsoluteUri));
        Assert.That(target.Url, Does.Not.Contain("LOCAL_APPLICATION"));
    }

    [TestCase("https://untrusted.invalid/callback")]
    [TestCase("http://0.0.0.0:9000/callback")]
    [TestCase("http://127.0.0.1:9000/callback?code=sentinel")]
    public void RejectsUnsafeRedirects(string redirect)
    {
        using var session = new EntraInteractiveOAuthSession();
        var options = Options(session, new Uri(redirect));
        Assert.That(() => ConnectionAuthentication.OAuthInteractive(options), Throws.ArgumentException);
    }

    [Test]
    public void CompetingOAuthQuerySelectorsAreRejectedBeforeSessionConfiguration()
    {
        using var session = new EntraInteractiveOAuthSession();
        var target = new ConnectionUrl("mssql://localhost/app?OAuthClientSecret=sentinel", Registry())
            .WithAuthentication(ConnectionAuthentication.OAuthInteractive(Options(session)));
        Assert.That(async () => await target.ConnectAsync(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("CredentialConflict"));
    }

    [Test, Explicit("Requires a human, a registered public client and an Entra-enabled SQL database.")]
    public async Task EntraLiveInteractiveQuery()
    {
        using var session = new EntraInteractiveOAuthSession();
        var strategy = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
        {
            ClientId = Required("DUBURL_ENTRA_PUBLIC_CLIENT_ID"), Authority = new(Required("DUBURL_ENTRA_AUTHORITY")),
            RedirectUri = new(Required("DUBURL_ENTRA_REDIRECT")), Scopes = ["https://database.windows.net/.default"],
            Session = session, AllowInteraction = true
        });
        await using var connection = await new ConnectionUrl(Required("DUBURL_ENTRA_SQL_URL"), Registry()).WithAuthentication(strategy).OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT 1";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
    }

    [Test, Explicit("Requires a human and a Snowflake account with local application OAuth enabled.")]
    public async Task SnowflakeLiveInteractiveQuery()
    {
        var account = Required("DUBURL_SNOWFLAKE_ACCOUNT");
        using var session = new SnowflakeInteractiveOAuthSession(account);
        var strategy = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
        {
            ClientId = "LOCAL_APPLICATION", Authority = new("https://" + account + ".snowflakecomputing.com"),
            RedirectUri = new("http://127.0.0.1:41589/callback/"), Scopes = ["session:role:" + Required("DUBURL_SNOWFLAKE_ROLE")],
            Session = session, AllowInteraction = true
        });
        await using var connection = await new ConnectionUrl(Required("DUBURL_SNOWFLAKE_OAUTH_URL"), Registry()).WithAuthentication(strategy).OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT 1";
        Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
    }

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value)) Assert.Ignore("Interactive live configuration is absent.");
        return value!;
    }
    private static InteractiveOAuthOptions Options(IInteractiveOAuthSession session, Uri? redirect = null) => new()
    {
        ClientId = "00000000-0000-0000-0000-000000000000", Authority = new("https://login.microsoftonline.com/tenant"),
        RedirectUri = redirect ?? new("http://localhost"), Scopes = ["https://database.windows.net/.default"], Session = session
    };
    private static ISchemeRegistry Registry()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Snowflake.Data", SnowflakeDbFactory.Instance);
        return new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }
}
