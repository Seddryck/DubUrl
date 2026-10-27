using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class TokenAuthenticationTests
{
    private static ISchemeRegistry Registry()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        return new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }

    [Test]
    public async Task InstallsOneStableLazyCallbackAndPreservesNativeExpiration()
    {
        var source = new Source();
        var target = new ConnectionUrl("mssql://localhost/app", Registry()).WithAuthentication(SqlServerAuthentication.TokenCallback(source));
        _ = target.Parse();
        _ = target.Dialect;
        using var first = (SqlConnection)await target.ConnectAsync();
        using var second = (SqlConnection)await target.ConnectAsync();
        Assert.That(source.Calls, Is.Zero);
        Assert.That(first.AccessTokenCallback, Is.SameAs(second.AccessTokenCallback));
        var token = await first.AccessTokenCallback!(null!, CancellationToken.None);
        Assert.That(token.AccessToken, Is.EqualTo("sentinel"));
        Assert.That(token.ExpiresOn, Is.EqualTo(source.Expiration));
        Assert.That(first.ConnectionString, Does.Not.Contain("sentinel"));
        Assert.That(new SqlConnectionStringBuilder(first.ConnectionString).IntegratedSecurity, Is.False);
        Assert.That(new SqlConnectionStringBuilder(first.ConnectionString).TrustServerCertificate, Is.False);
    }

    [Test]
    public async Task CallbackFailureAndCancellationAreSafe()
    {
        using var connection = (SqlConnection)new ConnectionUrl("mssql://localhost/app", Registry())
            .WithAuthentication(SqlServerAuthentication.TokenCallback(new Source { Fail = true })).Connect();
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await connection.AccessTokenCallback!(null!, CancellationToken.None));
        Assert.That(error!.ToString(), Does.Not.Contain("sentinel"));
        Assert.That(error.InnerException, Is.Null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(async () => await connection.AccessTokenCallback!(null!, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    [TestCase("mssql://alice@localhost/app")]
    [TestCase("mssql://localhost/app?Authentication=Active%20Directory%20Interactive")]
    [TestCase("mssql://localhost/app?AccessToken=sentinel")]
    public void RejectsCompetingModes(string url)
        => Assert.That(() => new ConnectionUrl(url, Registry()).WithAuthentication(SqlServerAuthentication.TokenCallback(new Source())).Connect(),
            Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("CredentialConflict"));

    [Test]
    public void CertificateSourceValidatesAudienceAndAuthorityWithoutAcquisition()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=fixture", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        var source = new EntraCertificateTokenSource("00000000-0000-0000-0000-000000000000", new("https://login.microsoftonline.com/tenant"), new("https://database.windows.net/"), certificate);
        Assert.That(() => source.ValidateRequest("https://database.windows.net/", "https://login.microsoftonline.com/tenant"), Throws.Nothing);
        Assert.That(() => source.ValidateRequest("https://untrusted.invalid/", null), Throws.TypeOf<ConnectionAuthenticationException>());
        Assert.That(() => source.ValidateRequest("https://database.windows.net/", "https://untrusted.invalid/tenant"), Throws.TypeOf<ConnectionAuthenticationException>());
        Assert.That(certificate.HasPrivateKey, Is.True);
    }

    private sealed class Source : ISqlServerTokenSource
    {
        public bool Fail { get; init; }
        public int Calls { get; private set; }
        public DateTimeOffset Expiration { get; } = DateTimeOffset.UtcNow.AddHours(1);
        public ValueTask<SqlAuthenticationToken> GetTokenAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Fail) throw new InvalidOperationException("sentinel");
            return new(new SqlAuthenticationToken("sentinel", Expiration));
        }
    }
}
