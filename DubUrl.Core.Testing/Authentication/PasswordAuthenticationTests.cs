using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

[NonParallelizable]
public class PasswordAuthenticationTests
{
    private ISchemeRegistry registry = null!;
    [SetUp]
    public void Setup()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("NReco.PrestoAdo", NReco.PrestoAdo.PrestoDbFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
        registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
    }

    [TestCase("mssql://localhost/app")]
    [TestCase("postgres://localhost/app")]
    [TestCase("mysql://localhost/app")]
    public async Task ResolvesPerConnectionWithoutChangingUrlOrCachingSecrets(string url)
    {
        var source = new RotatingSource();
        var original = new ConnectionUrl(url, registry);
        var target = original.WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
        _ = target.Dialect;
        Assert.That(source.Calls, Is.Zero);
        Assert.That(target.Url, Is.EqualTo(url));
        using var first = await target.ConnectAsync();
        using var second = await target.ConnectAsync();
        Assert.That(first.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(first.ConnectionString, Does.Contain("sentinel1"));
        Assert.That(second.ConnectionString, Does.Contain("sentinel2"));
        Assert.That(source.Calls, Is.EqualTo(2));
        Assert.That(source.Context!.ToString(), Does.Not.Contain("localhost"));
        if (!url.StartsWith("mysql", StringComparison.Ordinal))
            Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
    }

    [TestCase("postgres://alice@localhost/app")]
    [TestCase("mssql://alice@localhost/app")]
    public void SupplementsPasswordAndPreservesSpecialCharacters(string url)
    {
        using var connection = new ConnectionUrl(url, registry)
            .WithAuthentication(ConnectionAuthentication.Password("x;Password='y@%+")) .Connect();
        var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        Assert.That(builder["Password"], Is.EqualTo("x;Password='y@%+"));
    }

    [TestCase("postgres://alice:@localhost/app")]
    [TestCase("postgres://alice:existing@localhost/app")]
    [TestCase("postgres://localhost/app?Pwd=")]
    [TestCase("postgres://localhost/app?User%20ID=alice")]
    [TestCase("mssql://localhost/app?Trusted_Connection=true")]
    [TestCase("mssql://localhost/app?Authentication=Active%20Directory%20Interactive")]
    public void RejectsCompetingSourcesBeforeResolution(string url)
    {
        var source = new RotatingSource();
        var target = new ConnectionUrl(url, registry).WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
        var failure = Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(failure!.Code, Is.EqualTo("CredentialConflict"));
        Assert.That(source.Calls, Is.Zero);
    }

    [TestCase("alice", true)]
    [TestCase("Alice", false)]
    public void ComparesUsernamesOrdinally(string supplied, bool success)
    {
        var target = new ConnectionUrl("postgres://alice@localhost/app", registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(supplied, "sentinel"));
        if (success) { using var connection = target.Connect(); }
        else Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("CredentialConflict"));
    }

    [Test]
    public async Task AsyncOnlySourceIsNeverBlockedSynchronously()
    {
        var target = new ConnectionUrl("postgres://localhost/app", registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(new AsyncSource()));
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("SynchronousCredentialsRequired"));
        using var connection = await target.ConnectAsync();
        Assert.That(connection.ConnectionString, Does.Contain("sentinel"));
    }

    [Test]
    public async Task SourceFailuresNeverRetainUnsafeExceptionChains()
    {
        var target = new ConnectionUrl("postgres://localhost/app", registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(new FailingSource()));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.ConnectAsync());
        Assert.That(error!.Code, Is.EqualTo("CredentialResolutionFailed"));
        Assert.That(error.ToString(), Does.Not.Contain("sentinel"));
        Assert.That(error.InnerException, Is.Null);
        Assert.That(new UsernamePasswordCredentials("alice", "sentinel").ToString(), Does.Not.Contain("sentinel"));
        Assert.That(ConnectionAuthentication.Password("sentinel").ToString(), Does.Not.Contain("sentinel"));
    }

    [Test]
    public void CancellationBeforeResolutionIsPreserved()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new RotatingSource();
        var target = new ConnectionUrl("postgres://localhost/app", registry).WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
        Assert.That(async () => await target.OpenAsync(cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(source.Calls, Is.Zero);
    }

    [Test]
    public void ParserPreservesAbsentAndExplicitEmpty()
    {
        Assert.That(ConnectionUri.Parse("postgres://alice@localhost/app").HasPassword, Is.False);
        Assert.That(ConnectionUri.Parse("postgres://alice:@localhost/app").HasPassword, Is.True);
        Assert.That(ConnectionUri.Parse("postgres://localhost/app").HasUsername, Is.False);
        Assert.That(ConnectionUri.Parse("postgres://alice@localhost/app").HasUsername, Is.True);
    }

    [TestCase("trino://localhost/pg/public?UseSsl=true", "User", "Password", false)]
    [TestCase("trino://localhost/pg/public?UseSsl=true", "User", "Password", true)]
    [TestCase("odbc+mssql://localhost/app?Driver=FixtureDriver", "UID", "PWD", false)]
    [TestCase("odbc+mssql://localhost/app?Driver=FixtureDriver", "UID", "PWD", true)]
    [TestCase("odbc+drill://localhost/dfs?Driver=FixtureDriver", "UID", "PWD", false)]
    [TestCase("odbc+drill://localhost/dfs?Driver=FixtureDriver", "UID", "PWD", true)]
    public async Task RuntimeCredentialsReachExistingProviderMapping(string url, string userKey, string passwordKey, bool asynchronous)
    {
        const string password = "x;Password='y@%+";
        var target = new ConnectionUrl(url, registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword("alice", password));
        using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        if (connection is NReco.PrestoAdo.PrestoConnection)
        {
            // NReco 1.1.0 does not retain its assigned ConnectionString; inspect the actual client configuration.
            var config = typeof(NReco.PrestoAdo.PrestoConnection)
                .GetField("config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(connection)!;
            Assert.That(config.GetType().GetProperty(userKey)!.GetValue(config), Is.EqualTo("alice"));
            Assert.That(config.GetType().GetProperty(passwordKey)!.GetValue(config), Is.EqualTo(password));
        }
        else
        {
            var builder = new System.Data.Odbc.OdbcConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            Assert.That(builder[userKey], Is.EqualTo("alice"));
            // ODBC retains the braces used to quote semicolons in a parsed value.
            Assert.That(builder[passwordKey], Is.EqualTo("{" + password + "}"));
            if (url.StartsWith("odbc+drill", StringComparison.Ordinal))
                Assert.That(builder["AuthenticationType"], Is.EqualTo("Plain"));
        }
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(target.Url, Is.EqualTo(url));
        Assert.That(target.Parse(), Does.Not.Contain(password));
    }

    [Test]
    public async Task CustomProviderDoesNotNeedACorePasswordAllowlistEntry()
    {
        const string invariant = "Custom.PasswordProvider";
        var mapper = (BaseMapper)registry.GetMapper("mssql");
        mapper.SetProviderMetadata(invariant, null);
        var catalog = new ProviderCatalogBuilder();
        catalog.AddProvider(new ProviderRegistration("Custom", ["custom"], invariant,
            () => SqlClientFactory.Instance, mapper.GetDialect(), mapper.GetParametrizer, _ => mapper));
        var target = new ConnectionUrl("custom://localhost/app", catalog.Build())
            .WithAuthentication(ConnectionAuthentication.UsernamePassword("alice", "sentinel"));
        using var connection = await target.ConnectAsync();
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
        Assert.That(builder.UserID, Is.EqualTo("alice"));
        Assert.That(builder.Password, Is.EqualTo("sentinel"));
    }

    [TestCase(true, true), TestCase(true, false)]
    [TestCase(false, true), TestCase(false, false)]
    public async Task FluentConfigurationPreservesRuntimeCredentialsInEitherOrder(bool authenticationFirst, bool asynchronous)
    {
        var source = new RotatingSource();
        var original = new ConnectionUrl("mssql://localhost/app", registry);
        ConnectionUrl Configure(ConnectionUrl target) => target.WithApplicationName("Integration")
            .WithOptional(options => options.WithApplicationName("Optional").WithPooling(new() { Enabled = false }));
        var target = authenticationFirst
            ? Configure(original.WithAuthentication(ConnectionAuthentication.UsernamePassword(source)))
            : Configure(original).WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
        Assert.That(source.Calls, Is.Zero);
        using var first = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        using var second = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        var mapped = new SqlConnectionStringBuilder(first.ConnectionString);
        Assert.That(mapped.ApplicationName, Is.EqualTo("Integration"));
        Assert.That(mapped.Pooling, Is.False);
        Assert.That(mapped.UserID, Is.EqualTo("alice"));
        Assert.That(mapped.Password, Is.EqualTo("sentinel1"));
        Assert.That(new SqlConnectionStringBuilder(second.ConnectionString).Password, Is.EqualTo("sentinel2"));
        Assert.That(source.Calls, Is.EqualTo(2));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
        Assert.That(original.Parse(), Does.Not.Contain("Integration"));
    }

    [TestCase(true), TestCase(false)]
    public void StrictReadOnlySurvivesAuthenticationAndOptionalCallsBeforeAcquisition(bool authenticationFirst)
    {
        var source = new RotatingSource();
        var original = new ConnectionUrl("mssql://localhost/app", registry);
        var target = authenticationFirst
            ? original.WithAuthentication(ConnectionAuthentication.UsernamePassword(source)).WithReadOnly()
            : original.WithReadOnly().WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
        target = target.WithOptional(options => options.WithApplicationName("Optional"));
        Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(source.Calls, Is.Zero);
        Assert.Throws<NotSupportedException>(() => target.Parse());
    }

    [Test]
    public async Task OptionalUnsupportedSettingsPreserveRuntimeCredentialResolution()
    {
        var source = new RotatingSource();
        var target = new ConnectionUrl("trino://localhost/pg/public?UseSsl=true", registry)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(source))
            .WithOptional(options => options.WithApplicationName("Optional"));
        using var connection = await target.ConnectAsync();
        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(target.SkippedOptions.Single().Option, Is.EqualTo(ConnectionConfigurationOption.ApplicationName));
        Assert.That(target.Parse(), Does.Not.Contain("sentinel"));
    }

    private sealed class RotatingSource : ISynchronousUsernamePasswordCredentialSource
    {
        public int Calls { get; private set; }
        public AuthenticationContext? Context { get; private set; }
        public UsernamePasswordCredentials Resolve(AuthenticationContext context)
        { Context = context; return new("alice", "sentinel" + ++Calls); }
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return new(Resolve(context)); }
    }
    private sealed class AsyncSource : IUsernamePasswordCredentialSource
    {
        public async ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return new("alice", "sentinel"); }
    }
    private sealed class FailingSource : IUsernamePasswordCredentialSource
    {
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("sentinel", new Exception("sentinel"));
    }
}
