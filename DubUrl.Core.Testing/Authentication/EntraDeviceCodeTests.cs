using System.Net;
using System.Text;
using System.Text.Json;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

/// <summary>Actual MSAL protocol against an in-memory HTTP handler; not a live Entra or database login.</summary>
[NonParallelizable]
public class EntraDeviceCodeTests
{
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "11111111-1111-1111-1111-111111111111";
    private static string Authority => "https://login.microsoftonline.com/" + Tenant;
    private static InteractiveOAuthOptions Options(IInteractiveOAuthSession session, bool interaction = true) => new()
    {
        ClientId = Client, Authority = new(Authority), RedirectUri = new("http://localhost"), Scopes = ["https://database.windows.net/.default"],
        Session = session, AllowInteraction = interaction, AcquisitionTimeoutSeconds = 10
    };
    private static SqlAuthenticationParameters Request() => new(SqlAuthenticationMethod.ActiveDirectoryInteractive, "fixture", "app",
        "https://database.windows.net/", Authority + "/oauth2/authorize", null, null, Guid.NewGuid(), 30);
    private static async Task<SqlConnection> Configure(EntraInteractiveOAuthSession session, bool interaction = true)
    {
        var connection = new SqlConnection("Data Source=fixture;Initial Catalog=app;Pooling=false");
        await session.ConfigureAsync(connection, new("Microsoft.Data.SqlClient", "mssql", "fixture", null, "app", null), Options(session, interaction));
        return connection;
    }
    [Test]
    public async Task DeviceCodeSdkAcquiresThenUsesSilentCacheAndExplicitlyReacquires()
    {
        using var http = new Protocol();
        var prompts = 0;
        using var session = new EntraInteractiveOAuthSession(true, (url, code, expires, token) =>
        {
            prompts++;
            Assert.That(url, Is.EqualTo("https://microsoft.com/devicelogin"));
            Assert.That(code, Is.EqualTo("ABCD-1234"));
            Assert.That(expires, Is.GreaterThan(DateTimeOffset.UtcNow));
            return Task.CompletedTask;
        }, http);
        using var connection = await Configure(session);
        Assert.That(http.Requests, Is.Empty, "Closed configuration must make no network request.");
        var first = await connection.AccessTokenCallback!(Request(), CancellationToken.None);
        var cached = await connection.AccessTokenCallback!(Request(), CancellationToken.None);
        Assert.That(cached.AccessToken == first.AccessToken, Is.True, "Token values must not be printed.");
        Assert.That(prompts, Is.EqualTo(1));
        Assert.That(http.DeviceRequests, Is.EqualTo(1));
        await session.ClearCacheAsync();
        var next = await connection.AccessTokenCallback!(Request(), CancellationToken.None);
        Assert.That(next.AccessToken != first.AccessToken, Is.True);
        Assert.That(prompts, Is.EqualTo(2));
    }
    [Test]
    public async Task DeviceCodeSdkRefreshesExpiredTokenWithoutAnotherDevicePrompt()
    {
        using var http = new Protocol { ShortFirstToken = true };
        var prompts = 0;
        using var session = new EntraInteractiveOAuthSession(true, (_, _, _, _) => { prompts++; return Task.CompletedTask; }, http);
        using var connection = await Configure(session);
        var first = await connection.AccessTokenCallback!(Request(), CancellationToken.None);
        await Task.Delay(1500);
        var refreshed = await connection.AccessTokenCallback!(Request(), CancellationToken.None);
        Assert.That(refreshed.AccessToken != first.AccessToken, Is.True);
        Assert.That(prompts, Is.EqualTo(1));
        Assert.That(http.RefreshRequests, Is.EqualTo(1));
    }
    [Test]
    public async Task DeviceCodeDenialIsSanitized()
    {
        using var http = new Protocol { Denied = true };
        using var session = new EntraInteractiveOAuthSession(true, (_, _, _, _) => Task.CompletedTask, http);
        using var connection = await Configure(session);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await connection.AccessTokenCallback!(Request(), CancellationToken.None));
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.AuthenticationFailed));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(error.ToString(), Does.Not.Contain("private-error-description"));
    }
    [Test]
    public async Task DeviceCodeUserCancellationPropagates()
    {
        using var http = new Protocol();
        using var cancelled = new CancellationTokenSource();
        using var session = new EntraInteractiveOAuthSession(true, (_, _, _, _) => { cancelled.Cancel(); return Task.CompletedTask; }, http);
        using var connection = await Configure(session);
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await connection.AccessTokenCallback!(Request(), cancelled.Token));
        Assert.That(http.DeviceRequests, Is.EqualTo(1));
    }
    [Test]
    public async Task DeviceCodeRequiresInteractionBeforeMakingRequests()
    {
        using var http = new Protocol();
        using var session = new EntraInteractiveOAuthSession(true, (_, _, _, _) => Task.CompletedTask, http);
        using var connection = await Configure(session, interaction: false);
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await connection.AccessTokenCallback!(Request(), CancellationToken.None));
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.InteractionRequired));
        Assert.That(http.Requests, Is.Empty);
    }
    [Test]
    public async Task MsalUserCancelledSignalRemainsCancellationWithoutSdkDetails()
    {
        using var http = new Protocol();
        using var session = new EntraInteractiveOAuthSession(true, (_, _, _, _) =>
            throw new MsalClientException(MsalError.AuthenticationCanceledError, "private-browser-details"), http);
        using var connection = await Configure(session);
        var error = await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await connection.AccessTokenCallback!(Request(), CancellationToken.None));
        Assert.That(error!.ToString(), Does.Not.Contain("private-browser-details"));
    }
    private sealed class Protocol : HttpMessageHandler, IMsalHttpClientFactory
    {
        private readonly HttpClient client;
        internal List<string> Requests { get; } = [];
        internal int DeviceRequests { get; private set; }
        internal int RefreshRequests { get; private set; }
        private int tokens;
        internal bool ShortFirstToken { get; init; }
        internal bool Denied { get; init; }
        internal Protocol() => client = new(this, disposeHandler: false);
        public HttpClient GetHttpClient() => client;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            if (path.EndsWith("/instance", StringComparison.Ordinal))
                return Reply(new { tenant_discovery_endpoint = Authority + "/v2.0/.well-known/openid-configuration", metadata = new[]
                    { new { preferred_network = "login.microsoftonline.com", preferred_cache = "login.windows.net", aliases = new[] { "login.microsoftonline.com", "login.windows.net" } } } });
            if (path.Contains("openid-configuration", StringComparison.Ordinal))
                return Reply(new { authorization_endpoint = Authority + "/oauth2/v2.0/authorize", token_endpoint = Authority + "/oauth2/v2.0/token",
                    device_authorization_endpoint = Authority + "/oauth2/v2.0/devicecode", issuer = Authority + "/v2.0", jwks_uri = Authority + "/discovery/v2.0/keys" });
            if (path.EndsWith("/devicecode", StringComparison.Ordinal))
            {
                DeviceRequests++;
                return Reply(new { device_code = "private-device-code", user_code = "ABCD-1234", verification_uri = "https://microsoft.com/devicelogin",
                    verification_url = "https://microsoft.com/devicelogin", expires_in = 900, interval = 1, message = "private-sdk-message" });
            }
            Assert.That(path.EndsWith("/token", StringComparison.Ordinal), Is.True, "Only known in-memory protocol endpoints are permitted.");
            if (Denied) return Reply(new { error = "authorization_declined", error_description = "private-error-description" }, HttpStatusCode.BadRequest);
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (body.Contains("grant_type=refresh_token", StringComparison.Ordinal)) RefreshRequests++;
            tokens++;
            var id = Encode(new { alg = "none" }) + "." + Encode(new { aud = Client, iss = Authority + "/v2.0", tid = Tenant,
                oid = "33333333-3333-3333-3333-333333333333", preferred_username = "fixture@example.invalid", sub = "fixture",
                exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() }) + ".";
            return Reply(new { token_type = "Bearer", access_token = "qa-access-" + tokens, refresh_token = "qa-refresh",
                id_token = id, client_info = Encode(new { uid = "33333333-3333-3333-3333-333333333333", utid = Tenant }),
                expires_in = ShortFirstToken && tokens == 1 ? 1 : 3600, scope = "https://database.windows.net/.default" });
        }
        private static string Encode(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static HttpResponseMessage Reply(object data, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json") };
        protected override void Dispose(bool disposing) { if (disposing) client.Dispose(); base.Dispose(disposing); }
    }
}
