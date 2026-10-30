using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Provider.FirebirdSql.Mapping;
using DubUrl.Provider.Trino.Mapping;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;
using Moq;

namespace DubUrl.Core.Testing;

public class SharedConnectionConfigurationTests
{
    private static ConnectionUrl Target(string scheme, Type mapper, DbProviderFactory factory, string options = "")
    {
        var catalog = new ProviderCatalogBuilder();
        var mappings = AttributeProviderModule.Discover(mapper.Assembly).Where(m => m.MapperType == mapper).ToArray();
        if (mappings.Length == 0)
            mappings = new NativeMapperIntrospector([mapper.Assembly]).LocateAlternative().Where(m => m.MapperType == mapper)
                .Select(m => m with { ConnectionConfiguration = mapper.GetCustomAttributes(typeof(ConnectionConfigurationAttribute), false)
                    .Cast<ConnectionConfigurationAttribute>().Single().Create() }).ToArray();
        ProviderCatalogBuilder.RegisterMappings(catalog, mappings, _ => factory);
        return new($"{scheme}://{(scheme == "questdb" ? "alice:test-only" : "alice")}@db.example/{(scheme == "questdb" ? "qdb" : "app")}{options}", catalog.Build());
    }

    [TestCase("postgres", typeof(PostgresqlMapper), "Npgsql")]
    [TestCase("timescale", typeof(TimescaleMapper), "Npgsql")]
    [TestCase("cockroach", typeof(CockRoachMapper), "Npgsql")]
    [TestCase("crate", typeof(CrateDbMapper), "Npgsql")]
    [TestCase("questdb", typeof(QuestDbMapper), "Npgsql")]
    [TestCase("mysql", typeof(MySqlConnectorMapper), "MySql")]
    [TestCase("mariadb", typeof(MariaDbConnectorMapper), "MySql")]
    [TestCase("mysql", typeof(MySqlDataMapper), "ConnectorNet")]
    [TestCase("singlestore", typeof(SingleStoreMapper), "SingleStore")]
    public void NativeTls_RequiresFullVerificationAndPreservesUrl(string scheme, Type mapper, string provider)
    {
        DbProviderFactory factory = provider switch
        {
            "Npgsql" => NpgsqlFactory.Instance,
            "MySql" => MySqlConnectorFactory.Instance,
            "ConnectorNet" => MySql.Data.MySqlClient.MySqlClientFactory.Instance,
            _ => SingleStoreConnector.SingleStoreConnectorFactory.Instance
        };
        var original = Target(scheme, mapper, factory);
        var unchanged = original.Parse();
        var target = original.WithEncryption(ConnectionEncryption.Tls("server-ca.pem"));
        using var connection = (DbConnection)target.Connect();
        var native = factory.CreateConnectionStringBuilder()!;
        native.ConnectionString = connection.ConnectionString;
        Assert.That(native["SSL Mode"].ToString(), Is.EqualTo("VerifyFull"));
        Assert.That(native[provider == "Npgsql" ? "Root Certificate" : provider == "ConnectorNet" ? "SslCa" : "SSL CA"], Is.EqualTo(Path.GetFullPath("server-ca.pem")));
        Assert.That(target.Url, Is.EqualTo(original.Url));
        Assert.That(original.Parse(), Is.EqualTo(unchanged));
    }

    [TestCase("?Encrypt=False")]
    [TestCase("?TrustServerCertificate=True")]
    [TestCase("?HostNameInCertificate=another.example")]
    public void SqlTls_RejectsVerificationConflicts(string options)
        => Assert.Throws<ArgumentException>(() => Target("mssql", typeof(MsSqlServerMapper), SqlClientFactory.Instance, options)
            .WithEncryption(ConnectionEncryption.Tls()).Parse());

    [Test]
    public void SqlTls_UsesSystemTrustAndPreservesStrict()
    {
        var target = Target("mssql", typeof(MsSqlServerMapper), SqlClientFactory.Instance, "?Encrypt=Strict")
            .WithEncryption(ConnectionEncryption.Tls());
        var native = new SqlConnectionStringBuilder(target.Parse());
        Assert.That(native.Encrypt.ToString(), Is.EqualTo("Strict"));
        Assert.That(native.TrustServerCertificate, Is.False);
        Assert.Throws<NotSupportedException>(() => target.WithEncryption(ConnectionEncryption.Tls("ca.pem")).Parse());
    }

    [TestCase("?SSL%20Mode=Prefer")]
    [TestCase("?SSL%20Mode=Disable")]
    [TestCase("?GSS%20Encryption%20Mode=Require")]
    [TestCase("?Root%20Certificate=other.pem")]
    public void PostgresTls_RejectsConflictingUrl(string options)
        => Assert.Throws<ArgumentException>(() => Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance, options)
            .WithEncryption(ConnectionEncryption.Tls("ca.pem")).Parse());

    [TestCase(true), TestCase(false)]
    public async Task AuthenticationAndConfiguration_ComposeInEitherOrder(bool authenticationFirst)
    {
        var original = Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance);
        var strategy = ConnectionAuthentication.UsernamePassword("alice", "test-only");
        var target = authenticationFirst ? original.WithAuthentication(strategy) : original;
        target = target.WithEncryption(ConnectionEncryption.Tls()).WithTimeZone("Europe/Brussels")
            .WithApplicationName("Reporting;literal=1").WithConnectionTimeout(TimeSpan.FromMilliseconds(1))
            .WithCommandTimeout(TimeSpan.Zero).WithPooling(new() { MaxSize = 12, IdleTimeout = TimeSpan.FromSeconds(30), MaxLifetime = TimeSpan.FromSeconds(60) });
        if (!authenticationFirst) target = target.WithAuthentication(strategy);
        await using var connection = await target.ConnectAsync();
        var native = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        Assert.That(native.SslMode, Is.EqualTo(SslMode.VerifyFull));
        Assert.That(native.Timezone, Is.EqualTo("Europe/Brussels"));
        Assert.That(native.Timeout, Is.EqualTo(1));
        Assert.That(native.ConnectionIdleLifetime, Is.EqualTo(30));
        Assert.That(native.ConnectionLifetime, Is.EqualTo(60));
        Assert.That(native.ApplicationName, Is.EqualTo("Reporting;literal=1"));
        using var command = connection.CreateCommand();
        Assert.That(command.CommandTimeout, Is.Zero);
        command.CommandTimeout = 7;
        Assert.That(command.CommandTimeout, Is.EqualTo(7));
        Assert.That(target.Url, Is.EqualTo(original.Url));
    }

    [Test]
    public void TimeZone_RepeatedCallsOverrideUrlAndRejectAmbiguousOffset()
    {
        var original = Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance, "?Timezone=UTC");
        var target = original.WithTimeZone("Europe/Paris").WithTimeZone("Europe/Brussels");
        Assert.That(new NpgsqlConnectionStringBuilder(target.Parse()).Timezone, Is.EqualTo("Europe/Brussels"));
        Assert.Throws<NotSupportedException>(() => original.WithTimeZone("+02:00").Parse());
        Assert.Throws<ArgumentException>(() => original.WithTimeZone("UTC';DROP TABLE x;--"));
        Assert.Throws<ArgumentException>(() => original.WithTimeZone("+99:00"));
        Assert.Throws<ArgumentException>(() => original.WithTimeZone("12:00"));
        Assert.Throws<ArgumentException>(() => Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance, "?No%20Reset%20On%20Close=true")
            .WithTimeZone("Europe/Brussels").Parse());
    }

    [Test]
    public void Pooling_OmittedDefaultsAndValidation()
    {
        var target = Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance);
        Assert.Throws<ArgumentException>(() => target.WithPooling(new() { Enabled = false, IdleTimeout = TimeSpan.FromSeconds(1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WithPooling(new() { MaxLifetime = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentException>(() => Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance, "?Minimum%20Pool%20Size=20")
            .WithPooling(new() { MaxSize = 10 }).Parse());
        var mapped = new DbConnectionStringBuilder { ConnectionString = target.WithPooling(new() { MaxSize = 12 }).Parse() };
        Assert.That(mapped.ContainsKey("Connection Lifetime"), Is.False);
        Assert.That(mapped.ContainsKey("Connection Idle Lifetime"), Is.False);
    }

    [TestCase("mysql", typeof(MySqlDataMapper), "ConnectorNet")]
    [TestCase("singlestore", typeof(SingleStoreMapper), "SingleStore")]
    [TestCase("firebird", typeof(FirebirdSqlMapper), "Firebird")]
    public void AdditionalNativeDrivers_ConfigureRealCommandDefaults(string scheme, Type mapper, string provider)
    {
        DbProviderFactory factory = provider switch
        {
            "ConnectorNet" => MySql.Data.MySqlClient.MySqlClientFactory.Instance,
            "SingleStore" => SingleStoreConnector.SingleStoreConnectorFactory.Instance,
            _ => FirebirdSql.Data.FirebirdClient.FirebirdClientFactory.Instance
        };
        var target = Target(scheme, mapper, factory).WithConnectionTimeout(TimeSpan.FromSeconds(9))
            .WithCommandTimeout(TimeSpan.FromMilliseconds(1)).WithPooling(new() { MinSize = 1, MaxSize = 12, MaxLifetime = TimeSpan.FromSeconds(60) });
        using var connection = (DbConnection)target.Connect();
        Assert.That(connection.ConnectionTimeout, Is.EqualTo(9));
        using var first = connection.CreateCommand();
        using var second = connection.CreateCommand();
        Assert.That(first.CommandTimeout, Is.EqualTo(1));
        first.CommandTimeout = 8;
        Assert.That(first.CommandTimeout, Is.EqualTo(8));
        Assert.That(second.CommandTimeout, Is.EqualTo(1));
    }

    [Test]
    public void Oracle_UsesModulePropertyAndRequiresUnpooledSessionTimeZone()
    {
        var target = Target("oracle", typeof(DubUrl.Provider.Oracle.Mapping.OracleManagedDataAccessMapper),
            Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance).WithApplicationName("Reporting")
            .WithConnectionTimeout(TimeSpan.FromSeconds(9));
        using var connection = (DbConnection)target.Connect();
        Assert.That(connection, Is.InstanceOf<Oracle.ManagedDataAccess.Client.OracleConnection>());
        Assert.That(connection.ConnectionTimeout, Is.EqualTo(9));
        Assert.That(connection.ConnectionString, Does.Not.Contain("ModuleName"));
        Assert.Throws<NotSupportedException>(() => target.WithTimeZone("Europe/Brussels").Parse());
        Assert.DoesNotThrow(() => target.WithTimeZone("Europe/Brussels").WithPooling(new() { Enabled = false }).Parse());
        Assert.DoesNotThrow(() => target.WithPooling(new() { Enabled = false }).WithTimeZone("Europe/Brussels").Parse());
    }

    [Test]
    public void Db2_UsesActualClientApplicationNameAndOpeningTimeout()
    {
        var target = Target("db2", typeof(DubUrl.Provider.Db2.Mapping.Db2Mapper), IBM.Data.Db2.DB2Factory.Instance)
            .WithApplicationName("Reporting;literal=1").WithConnectionTimeout(TimeSpan.FromSeconds(9));
        var native = new IBM.Data.Db2.DB2ConnectionStringBuilder(target.Parse());
        Assert.That(native["ClientApplicationName"], Is.EqualTo("Reporting;literal=1"));
        Assert.That(Convert.ToInt32(native["Connect Timeout"]), Is.EqualTo(9));
    }

    [Test]
    public void Teradata_PreservesQueryBandAndUsesVerifiedTlsAndNativePool()
    {
        var original = Target("teradata", typeof(DubUrl.Provider.Teradata.Mapping.TeradataMapper), Teradata.Client.Provider.TdFactory.Instance,
            "?Query%20Band=Action%3Dexisting%3B");
        var target = original.WithApplicationName("Reporting").WithEncryption(ConnectionEncryption.Tls("ca.pem"))
            .WithConnectionTimeout(TimeSpan.FromSeconds(9)).WithPooling(new() { MinSize = 1, MaxSize = 12, IdleTimeout = TimeSpan.FromSeconds(30) });
        var native = new Teradata.Client.Provider.TdConnectionStringBuilder(target.Parse());
        Assert.That(native.QueryBand.ApplicationName, Is.EqualTo("Reporting"));
        Assert.That(native.QueryBand.Action, Is.EqualTo("existing"));
        Assert.That(native.SslMode.ToString().Replace("_", ""), Is.EqualTo("VERIFY-FULL"));
        Assert.That(native.SslCA, Is.EqualTo(Path.GetFullPath("ca.pem")));
        Assert.That(native.ConnectionPoolingTimeout, Is.EqualTo(30));
        Assert.Throws<NotSupportedException>(() => target.WithTimeZone("+02:00").Parse());
        Assert.DoesNotThrow(() => original.WithTimeZone("+02:00").WithPooling(new() { Enabled = false }).Parse());
        Assert.Throws<NotSupportedException>(() => original.WithTimeZone("Europe/Brussels").WithPooling(new() { Enabled = false }).Parse());
    }

    [TestCase("Odbc", "Driver={ODBC Driver 18 for SQL Server}", "Encrypt", "yes")]
    [TestCase("Odbc", "Driver={PostgreSQL Unicode(x64)}", "SSLmode", "verify-full")]
    [TestCase("Odbc", "Driver={MySQL ODBC 9.0 Unicode Driver}", "SSLMODE", "VERIFY_IDENTITY")]
    [TestCase("Odbc", "Driver={MariaDB ODBC 3.2 Driver}", "FORCETLS", "1")]
    [TestCase("OleDb", "Provider=MSOLEDBSQL19", "Use Encryption for Data", "Mandatory")]
    [TestCase("OleDb", "Provider=MSOLEDBSQL", "Use Encryption for Data", "True")]
    public void DriverTls_UsesExactVendorSettings(string transport, string native, string key, string expected)
    {
        if (transport == "OleDb" && !OperatingSystem.IsWindows()) Assert.Ignore("OLE DB requires Windows.");
        var builder = new DbConnectionStringBuilder(transport == "Odbc") { ConnectionString = native };
        new ConnectionConfigurationCapability { Tls = transport }.Apply(builder, new(ConnectionConfigurationOption.Encryption, ConnectionEncryption.Tls(), false));
        Assert.That(builder[key].ToString(), Is.EqualTo(expected));
        if (native.Contains("PostgreSQL")) Assert.That(builder["Pqopt"].ToString(), Does.Contain("sslrootcert='system'").And.Contain("gssencmode=disable"));
    }

    [TestCase("Driver=Unknown")]
    [TestCase("DSN=production")]
    [TestCase("Driver={ODBC Driver 18 for SQL Server};TrustServerCertificate=yes")]
    [TestCase("Driver={PostgreSQL Unicode};Pqopt=sslmode=disable")]
    public void DriverTls_RejectsOpaqueAndInsecureSettings(string native)
    {
        var builder = new DbConnectionStringBuilder(true) { ConnectionString = native };
        Assert.That(() => new ConnectionConfigurationCapability { Tls = "Odbc" }.Apply(builder,
            new(ConnectionConfigurationOption.Encryption, ConnectionEncryption.Tls(), false)), Throws.Exception);
    }

    [Test]
    public void Trino_TlsRejectsCustomCaAndInsecureUrl()
    {
        var target = Target("trino", typeof(TrinoMapper), NReco.PrestoAdo.PrestoDbFactory.Instance);
        Assert.That(new DbConnectionStringBuilder { ConnectionString = target.WithEncryption(ConnectionEncryption.Tls()).Parse() }["UseSsl"].ToString(), Is.EqualTo("True"));
        Assert.Throws<NotSupportedException>(() => target.WithEncryption(ConnectionEncryption.Tls("ca.pem")).Parse());
        Assert.Throws<ArgumentException>(() => Target("trino", typeof(TrinoMapper), NReco.PrestoAdo.PrestoDbFactory.Instance, "?UseSsl=false")
            .WithEncryption(ConnectionEncryption.Tls()).Parse());
    }

    [TestCase("sql:mysql", "SET SESSION time_zone = 'Europe/Brussels'")]
    [TestCase("sql:oracle", "ALTER SESSION SET TIME_ZONE = 'Europe/Brussels'")]
    [TestCase("sql:firebird", "SET TIME ZONE 'Europe/Brussels'")]
    public void SessionInitialization_RunsOnEveryOpen(string mapping, string sql)
    {
        var connection = new EventConnection();
        SessionTimeZone.Configure(connection, mapping, "Europe/Brussels");
        connection.Open(); connection.Close(); connection.Open();
        Assert.That(connection.Statements, Is.EqualTo(new[] { sql, sql }));
    }

    [Test]
    public void TeradataOffsetInitializationPreservesTheOffsetSign()
    {
        var connection = new EventConnection();
        SessionTimeZone.Configure(connection, "sql:teradata", "-02:00");
        connection.Open();
        Assert.That(connection.Statements.Single(), Is.EqualTo("SET TIME ZONE INTERVAL -'02:00' HOUR TO MINUTE"));
    }

    [Test]
    public async Task ConflictingTlsFailsBeforeCredentialAcquisition()
    {
        var credentials = new CountingCredentials();
        var target = Target("postgres", typeof(PostgresqlMapper), NpgsqlFactory.Instance, "?SSL%20Mode=Disable")
            .WithAuthentication(ConnectionAuthentication.UsernamePassword(credentials)).WithEncryption(ConnectionEncryption.Tls());
        await Assert.ThrowsAsync<ArgumentException>(async () => { await using var connection = await target.ConnectAsync(); });
        Assert.That(credentials.Calls, Is.Zero);
    }
    private sealed class CountingCredentials : IUsernamePasswordCredentialSource
    {
        internal int Calls { get; private set; }
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; return new(new UsernamePasswordCredentials("alice", "test-only")); }
    }

    [Test]
    public void SessionInitialization_DisposesOnFailureAndPreservesCancellation()
    {
        var failed = new EventConnection { Fail = true };
        SessionTimeZone.Configure(failed, "sql:mysql", "UTC");
        Assert.Throws<InvalidOperationException>(() => failed.Open());
        Assert.That(failed.WasDisposed, Is.True);
        var canceled = new EventConnection();
        SessionTimeZone.Configure(canceled, "sql:mysql", "UTC");
        SessionTimeZone.SetCancellation(canceled, new CancellationToken(true));
        Assert.Throws<OperationCanceledException>(() => canceled.Open());
        Assert.That(canceled.Statements, Is.Empty);
        Assert.That(canceled.WasDisposed, Is.True);
    }

    [TestCase(true), TestCase(false)]
    public async Task AuthenticatedTimeout_RemainsNativeAndDisposesFailedConnection(bool asynchronous)
    {
        var timeout = new TimeoutException("controlled opening timeout");
        var connection = new EventConnection { OpeningFailure = timeout };
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(f => f.CreateConnection()).Returns(connection);
        factory.Setup(f => f.CreateConnectionStringBuilder()).Returns(() => new NpgsqlConnectionStringBuilder());
        var target = Target("postgres", typeof(PostgresqlMapper), factory.Object)
            .WithAuthentication(ConnectionAuthentication.UsernamePassword("alice", "test-only"))
            .WithConnectionTimeout(TimeSpan.FromSeconds(1));
        if (asynchronous)
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(async () => { await using var opened = await target.OpenAsync(); });
            Assert.That(error, Is.SameAs(timeout));
        }
        else Assert.That(Assert.Throws<TimeoutException>(() => target.Open()), Is.SameAs(timeout));
        Assert.That(connection.WasDisposed, Is.True);
    }

    private sealed class EventConnection : DbConnection
    {
        private ConnectionState state;
        internal List<string> Statements { get; } = [];
        internal bool Fail { get; init; }
        internal Exception? OpeningFailure { get; init; }
        internal bool WasDisposed { get; private set; }
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = "";
        public override string Database => "app";
        public override string DataSource => "test";
        public override string ServerVersion => "1";
        public override ConnectionState State => state;
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        public override void Close() { state = ConnectionState.Closed; }
        public override void Open() { if (OpeningFailure is not null) throw OpeningFailure; state = ConnectionState.Open; OnStateChange(new(ConnectionState.Closed, state)); }
        protected override void Dispose(bool disposing) { WasDisposed = true; Close(); base.Dispose(disposing); }
        protected override DbCommand CreateDbCommand()
        {
            var command = new Mock<DbCommand>();
            command.SetupAllProperties();
            command.Setup(c => c.ExecuteNonQuery()).Returns(() =>
            {
                Statements.Add(command.Object.CommandText);
                if (Fail) throw new InvalidOperationException("test initialization failure");
                return 0;
            });
            return command.Object;
        }
    }
}
