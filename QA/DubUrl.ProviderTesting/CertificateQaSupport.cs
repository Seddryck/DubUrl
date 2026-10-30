using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal sealed class CertificateQaDriver(PasswordQaDriver driver)
{
    internal PasswordQaDriver Driver => driver;
    internal bool Supported => driver.Provider is "MySqlConnector" or "SingleStoreConnector"
        || (driver.Provider == "Npgsql" && (driver.Family is "postgresql" or "cockroachdb" or "timescale"));
    internal bool Supplemental => driver.Provider is "MySqlConnector" or "SingleStoreConnector";
    internal void RequireSupported()
    {
        if (!Supported) Assert.Ignore("#1699: " + Driver.Provider + "/" + Driver.Family + " has no supported native client-certificate strategy. File access, SQL tokens and unrelated TLS settings are not certificate login.");
    }
    internal void RequireSupplemental()
    {
        RequireSupported();
        if (!Supplemental) Assert.Ignore("#1699: supplemental database credentials are supported only by MySqlConnector/SingleStoreConnector, not this certificate-only adapter.");
    }
    internal string Location(string? weakMode = null, string? conflict = null)
    {
        var location = Driver.Location.Split('?')[0];
        if (Driver.Uri.HasUsername)
        {
            var position = location.IndexOf("://", StringComparison.Ordinal) + 3;
            location = location[..position] + Uri.EscapeDataString(Driver.Uri.Username) + "@" + location[position..];
        }
        var options = Driver.Uri.Options.Where(pair => Normalize(pair.Key) is not ("sslmode" or "pooling" or "trustservercertificate"))
            .Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)).ToList();
        options.Add("Pooling=false");
        if (weakMode is not null) options.Add("SSL%20Mode=" + NativeMode(weakMode));
        if (conflict is not null) options.Add(Uri.EscapeDataString(conflict) + "=" + Uri.EscapeDataString(TokenQaSource.Sentinel));
        return location + "?" + string.Join('&', options);
    }
    private string NativeMode(string value) => value switch
    {
        "disabled" => Driver.Provider == "Npgsql" ? "Disable" : "Disabled",
        "required" => Driver.Provider == "Npgsql" ? "Require" : "Required",
        _ => "VerifyCA"
    };
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    internal ConnectionUrl Target(X509Certificate2 certificate, TokenQaRegistry registry, IUsernamePasswordCredentialSource? source = null, string? location = null)
        => new ConnectionUrl(location ?? Location(), registry).WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate, new() { SupplementalCredentials = source }));
    internal static async Task AssertHookAsync(DbConnection connection, X509Certificate2 certificate)
    {
        using var other = TokenQaCertificates.Create();
        var collection = new X509CertificateCollection { other };
        var callback = connection.GetType().GetProperty("ProvideClientCertificatesCallback")?.GetValue(connection) as Delegate;
        Assert.That(callback, Is.Not.Null);
        var result = callback!.DynamicInvoke(collection);
        if (result is ValueTask valueTask) await valueTask;
        else if (result is Task task) await task;
        Assert.That(collection.Count, Is.EqualTo(1));
        Assert.That(ReferenceEquals(collection[0], certificate), Is.True, "The native hook must retain the caller object; certificate details are suppressed.");
        var settings = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        var mode = settings.Keys.Cast<string>().Single(key => Normalize(key) == "sslmode");
        Assert.That(settings[mode].ToString(), Is.EqualTo("VerifyFull").IgnoreCase);
    }
    internal static async Task AssertDisposedAsync(DbConnection connection)
    {
        Exception? failure = null;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await connection.OpenAsync(budget.Token); }
        catch (Exception error) { failure = error; }
        Assert.That(failure is ObjectDisposedException, Is.True, "A failed native connection must reject reopening after disposal; native diagnostics are suppressed.");
    }
}

internal sealed class CertificateQaScope : IAsyncDisposable
{
    private readonly bool pooling;
    internal TokenQaRegistry Registry { get; }
    internal CertificateQaScope(DubUrl.Mapping.ISchemeRegistry inner, bool pooling = false)
    {
        this.pooling = pooling;
        Registry = CreateRegistry(inner, pooling);
    }
    private static TokenQaRegistry CreateRegistry(DubUrl.Mapping.ISchemeRegistry inner, bool pooling)
    {
        var registry = new TokenQaRegistry(inner);
        registry.Options["Pooling"] = pooling;
        registry.Options["Application Name"] = "DubUrlCertQa_" + Guid.NewGuid().ToString("N");
        if (pooling) registry.Options["Maximum Pool Size"] = 1;
        return registry;
    }
    internal void ClearPools()
    {
        if (!pooling) return;
        foreach (var connection in Registry.Connections)
        {
            var native = connection.GetType();
            var clear = native.GetMethod("ClearPool", BindingFlags.Public | BindingFlags.Static, null, [native], null)
                ?? throw new AssertionException("#1699: the native adapter has no targeted pool cleanup API.");
            clear.Invoke(null, [connection]);
        }
    }
    public async ValueTask DisposeAsync()
    {
        try { ClearPools(); }
        finally { foreach (var connection in Registry.Connections) await connection.DisposeAsync(); }
    }
}
