using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Provider.DuckDb.Mapping;
using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.Native.Tests;

[NonParallelizable]
public class EncryptedConnectionTests
{
    [TestCase("v1.4.0")]
    [TestCase("v1.5.6")]
    [TestCase("1.4.1-dev")]
    public void Version_Supported(string version)
        => Assert.DoesNotThrow(() => EncryptedDuckDbConnection.RequireSupportedVersion(version));

    [TestCase("v1.3.2")]
    [TestCase("unknown")]
    public void Version_Unsupported(string version)
        => Assert.That(() => EncryptedDuckDbConnection.RequireSupportedVersion(version), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo(AuthenticationDiagnosticCodes.UnsupportedProviderVersion));

    [Test]
    public void Literals_EscapeQuotesAndRejectNul()
    {
        Assert.That(EncryptedDuckDbConnection.Literal("a'; SELECT 1; --"), Is.EqualTo("'a''; SELECT 1; --'"));
        Assert.Throws<ArgumentException>(() => EncryptedDuckDbConnection.Literal("a\0b"));
    }

    [Test]
    public void Metadata_ConsumesAndRedactsUrlKey_WithoutCreatingFile()
    {
        WithRegistry(registry =>
        {
            var key = Guid.NewGuid().ToString("N") + "%2B +&';";
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".duckdb");
            var target = new ConnectionUrl("duckdb:///" + path.Replace('\\', '/') + "?ENCRYPTION_KEY=" + Uri.EscapeDataString(key), registry);
            Assert.That(target.Parse(), Does.Not.Contain(key).And.Not.Contain("ENCRYPTION_KEY"));
            Assert.That(target.Url, Does.Not.Contain(Uri.EscapeDataString(key)));
            Assert.That(ConnectionUri.Parse("duckdb:///?ENCRYPTION_KEY=" + Uri.EscapeDataString(key)).ToString(), Does.Not.Contain(Uri.EscapeDataString(key)));
            using var connection = target.Connect();
            Assert.That(connection, Is.InstanceOf<DuckDBConnection>());
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(connection.ConnectionString, Does.Not.Contain(key));
            Assert.That(File.Exists(path), Is.False);
            Assert.Throws<NotSupportedException>(() => connection.ConnectionString = "DataSource=:memory:");
        });
    }

    [TestCase("ENCRYPTION_KEY=")]
    [TestCase("ENCRYPTION_KEY=x&encryption-key=y")]
    [TestCase("ENCRYPTION_KEY=x&password=y")]
    [TestCase("ENCRYPTION_KEY=x&access_token=y")]
    public void InvalidInputs_RejectBeforeCreation(string options)
        => WithRegistry(registry => Assert.Throws<ConnectionAuthenticationException>(() => new ConnectionUrl("duckdb:///missing.duckdb?" + options, registry).Connect()));

    [Test]
    public void UrlAndStrategy_RejectBeforeResolvingSource()
    {
        WithRegistry(registry =>
        {
            var source = new UnresolvedSource();
            var target = new ConnectionUrl("duckdb:///missing.duckdb?ENCRYPTION_KEY=x", registry)
                .WithAuthentication(ConnectionAuthentication.FilePassword(source));
            var error = Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
            Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialConflict));
            Assert.That(source.Calls, Is.Zero);
        });
    }

    [Test]
    public void StrategyMetadataAndPreparation_DoNotResolveSource()
    {
        WithRegistry(registry =>
        {
            var source = new UnresolvedSource();
            var target = new ConnectionUrl("duckdb:///missing.duckdb", registry).WithAuthentication(ConnectionAuthentication.FilePassword(source));
            target.ValidateAuthentication();
            target.ValidateConfiguration();
            _ = target.Parse(); _ = target.Dialect; _ = target.BulkCopyProvider;
            using var connection = target.Connect();
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(source.Calls, Is.Zero);
        });
    }

    [Test]
    public void DuplicateOptions_DisplayRemainsRedactedWhenParsingFails()
    {
        var key = Guid.NewGuid().ToString("N");
        var target = new ConnectionUrl("duckdb:///?ENCRYPTION_KEY=" + key + "&encryption_key=" + key, new SchemeRegistry([]));
        Assert.That(target.Url, Does.Not.Contain(key));
        Assert.Throws<ConnectionUriParseException>(() => target.Parse());
    }

    private static void WithRegistry(Action<ISchemeRegistry> test)
    {
        var existed = DbProviderFactories.TryGetFactory("DuckDB.NET.Data", out var previous);
        try
        {
            DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDBClientFactory.Instance);
            var builder = new ProviderCatalogBuilder();
            builder.AddModule(new AttributeProviderModule(typeof(DuckdbMapper).Assembly));
            test(builder.Build());
        }
        finally
        {
            if (existed) DbProviderFactories.RegisterFactory("DuckDB.NET.Data", previous!);
            else DbProviderFactories.UnregisterFactory("DuckDB.NET.Data");
        }
    }

    private sealed class UnresolvedSource : IFileSecretCredentialSource
    {
        public int Calls { get; private set; }
        public ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; throw new InvalidOperationException("This source must not be called during preparation."); }
    }
}
