using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using DubUrl.Authentication.SqlServer;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Observes actual driver instances without wrapping SqlConnection and changing native callback capability.</summary>
internal sealed class TokenQaRegistry(ISchemeRegistry inner) : ISchemeRegistry
{
    internal List<DbConnection> Connections { get; } = [];
    internal HashSet<DbConnection> Disposed { get; } = [];
    internal Dictionary<string, object> Options { get; } = [];
    public ResolvedConnection Resolve(ConnectionUri uri) => Observe(inner.Resolve(uri));
    private ResolvedConnection Observe(ResolvedConnection result)
    {
        var builder = result.ProviderFactory.CreateConnectionStringBuilder() ?? new DbConnectionStringBuilder();
        builder.ConnectionString = result.ConnectionString;
        foreach (var option in Options) builder[option.Key] = option.Value;
        return result with { ConnectionString = builder.ConnectionString, ProviderFactory = new Factory(this, result.ProviderFactory) };
    }
    public bool TryResolve(ConnectionUri uri, out ResolvedConnection? result, out ConnectionResolutionDiagnostic? diagnostic)
    {
        if (!inner.TryResolve(uri, out result, out diagnostic)) return false;
        result = Observe(result!);
        return true;
    }
    public IMapper GetMapper(string alias) => inner.GetMapper(alias);
    public IMapper GetMapper(SchemeKey scheme) => inner.GetMapper(scheme);
    public DbProviderFactory GetProviderFactory(SchemeKey scheme) => new Factory(this, inner.GetProviderFactory(scheme));
    public bool TryGetCapability<T>(SchemeKey scheme, out T? capability) where T : class => inner.TryGetCapability(scheme, out capability);
    public bool CanHandle(string scheme) => inner.CanHandle(scheme);
    internal void ClearOwnedPools()
    {
        foreach (var connection in Connections.OfType<SqlConnection>()) SqlConnection.ClearPool(connection);
    }
    private sealed class Factory(TokenQaRegistry owner, DbProviderFactory factory) : DbProviderFactory
    {
        public override DbConnection CreateConnection()
        {
            var connection = factory.CreateConnection() ?? throw new AssertionException("The actual QA provider created no connection.");
            owner.Connections.Add(connection);
            connection.Disposed += (_, _) => owner.Disposed.Add(connection);
            return connection;
        }
    }
}

internal sealed class TokenQaSource(ISqlServerTokenSource? inner = null) : ISqlServerTokenSource, IDisposable
{
    internal const string Sentinel = "qa-token-sentinel-+=/?";
    internal int Calls { get; private set; }
    internal bool Disposed { get; private set; }
    internal string? Behavior { get; init; }
    internal SqlAuthenticationParameters? Request { get; private set; }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<SqlAuthenticationToken> GetTokenAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken = default)
    {
        Calls++;
        Request = request;
        Entered.TrySetResult();
        if (Behavior == "cancel") await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (Behavior == "failure") throw new InvalidOperationException(Sentinel);
        if (Behavior == "expired") return new(Sentinel, DateTimeOffset.UtcNow.AddMinutes(-1));
        if (inner is null) throw new InvalidOperationException(Sentinel);
        return await inner.GetTokenAsync(request, cancellationToken);
    }
    public void Dispose() => Disposed = true;
    public override string ToString() => "TokenQaSource [REDACTED]";
}

internal static class TokenQaCertificates
{
    internal static X509Certificate2 Create(bool expired = false)
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest("CN=DubUrl-QA-only", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), expired ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddHours(1));
    }
}

/// <summary>Opt-in only. No existing ordinary SQL container or cached password login is substituted for Entra.</summary>
internal sealed class TokenQaLiveFixture : IDisposable
{
    internal X509Certificate2 Certificate { get; }
    internal TokenQaRegistry Registry { get; }
    internal string Url { get; }
    internal string ClientId { get; }
    internal Uri Authority { get; }
    internal Uri Resource { get; }
    internal TokenQaSource Source { get; }
    internal ConnectionUrl Target { get; }

    internal TokenQaLiveFixture(PasswordQaDriver driver, bool pooling = false)
    {
        if (Environment.GetEnvironmentVariable("DUBURL_ENTRA_QA") != "1")
            Assert.Ignore("#1698: live Entra QA is not enabled. Existing SQL QA is an ordinary container, not an Entra endpoint. No tenant/application/certificate fixture is injected by unchanged CI.");
        Url = Required("DUBURL_ENTRA_CERT_SQL_URL");
        var uri = ConnectionUri.Parse(Url);
        Assert.That(uri.Scheme.Canonical, Is.EqualTo("mssql"), "The opt-in fixture must select native SQL Server.");
        Assert.That(uri.HasUsername || uri.HasPassword, Is.False, "The opt-in URL must be credential-free.");
        ClientId = Required("DUBURL_ENTRA_CERT_CLIENT_ID");
        Authority = new Uri(Required("DUBURL_ENTRA_CERT_AUTHORITY"));
        Resource = new Uri(Required("DUBURL_ENTRA_CERT_RESOURCE"));
        Certificate = LoadCertificate(false);
        Registry = new TokenQaRegistry(driver.Registry);
        Registry.Options["Pooling"] = pooling;
        Registry.Options["Application Name"] = "DubUrlTokenQa_" + Guid.NewGuid().ToString("N");
        Registry.Options["Max Pool Size"] = 1;
        Registry.Options["Connect Timeout"] = 20;
        try
        {
            Source = CreateSource(Certificate);
            Target = TargetFor(Source);
        }
        catch { Certificate.Dispose(); throw; }
    }
    internal TokenQaSource CreateSource(X509Certificate2 certificate)
        => new(new EntraCertificateTokenSource(ClientId, Authority, Resource, certificate));
    internal ConnectionUrl TargetFor(TokenQaSource source) => new ConnectionUrl(Url, Registry).WithAuthentication(SqlServerAuthentication.TokenCallback(source));
    internal static X509Certificate2 LoadCertificate(bool replacement)
        => X509Certificate2.CreateFromPemFile(Required(replacement ? "DUBURL_ENTRA_ROTATION_PEM" : "DUBURL_ENTRA_CERT_PEM"),
            Required(replacement ? "DUBURL_ENTRA_ROTATION_KEY" : "DUBURL_ENTRA_CERT_KEY"));
    internal static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value)) Assert.Ignore("#1698: missing opt-in fixture setting " + name + ". No external credential values are printed.");
        return value!;
    }
    public void Dispose()
    {
        Registry.ClearOwnedPools();
        Certificate.Dispose();
        Source.Dispose();
    }
}
