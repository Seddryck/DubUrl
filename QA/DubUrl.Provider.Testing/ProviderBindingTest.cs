using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.PostgreSql.Sql;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using Moq;
using Moq.Protected;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Provider.Testing;

public class ProviderBindingTest
{
    [Test]
    public void SelectedProvider_CompilesAndExecutesWithPublicContractsOnly()
    {
        var parameters = new List<DbParameter>();
        var collection = new Mock<DbParameterCollection>();
        collection.Setup(x => x.Add(It.IsAny<object>())).Returns((object value) =>
        {
            parameters.Add((DbParameter)value);
            return parameters.Count - 1;
        });
        var command = new Mock<DbCommand>();
        command.SetupAllProperties();
        command.Protected().Setup<DbParameterCollection>("DbParameterCollection").Returns(collection.Object);
        command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(() =>
        {
            var parameter = new Mock<DbParameter>();
            parameter.SetupAllProperties();
            return parameter.Object;
        });
        // Observe the bound parameters at execution, rather than returning an unrelated constant.
        command.Setup(x => x.ExecuteScalar()).Returns(() =>
            (int)parameters.Single(parameter => parameter.ParameterName == "answer").Value!);
        var connection = new Mock<DbConnection>();
        connection.SetupAllProperties();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        connection.SetupGet(x => x.State).Returns(ConnectionState.Open);
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new NpgsqlConnectionStringBuilder());
        factory.Setup(x => x.CreateConnection()).Returns(connection.Object);

        var registry = CreateCatalog(() => factory.Object).Build();
        var url = new ConnectionUrl("PG://localhost:5432/demo?Application Name=extension", registry);
        var settings = new DbConnectionStringBuilder { ConnectionString = url.Parse() };
        using var database = new DatabaseUrl(url, new CommandProvisionerFactory(), NullQueryLogger.Instance);
        var result = database.ReadScalar(new ParameterizedQuery());

        Assert.Multiple(() =>
        {
            Assert.That(settings["Host"], Is.EqualTo("localhost"));
            Assert.That(settings["Port"].ToString(), Is.EqualTo("5432"));
            Assert.That(settings["Database"], Is.EqualTo("demo"));
            Assert.That(settings["Application Name"], Is.EqualTo("extension"));
            Assert.That(url.Dialect, Is.TypeOf<PgsqlDialect>());
            Assert.That(url.Dialect.Renderer.Render("Order", "identity"), Is.EqualTo("\"Order\""));
            Assert.That(url.Parametrizer, Is.TypeOf<NamedParametrizer>());
            Assert.That(connection.Object.ConnectionString, Is.EqualTo(url.Parse()));
            Assert.That(command.Object.CommandText, Is.EqualTo("SELECT @answer"));
            Assert.That(result, Is.EqualTo(42));
            Assert.That(parameters.Single().DbType, Is.EqualTo(DbType.Int32));
        });
        factory.Verify(x => x.CreateConnection(), Times.Once);
        connection.Verify(x => x.Open(), Times.Once);
        command.Verify(x => x.ExecuteScalar(), Times.Once);
    }

    [Test]
    public void ExistingFamily_UsesOnlySelectedProviderAndDriver()
    {
        var registry = new ProviderCatalogBuilder().AddModule(ConnectorFixture.Create()).Build();
        var url = new ConnectionUrl("pg://localhost/demo?Application Name=extension", registry);
        var settings = new NpgsqlConnectionStringBuilder(url.Parse());

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("pg"), Is.TypeOf<PostgresqlMapper>());
            Assert.That(registry.GetProviderFactory(["pg"]), Is.SameAs(NpgsqlFactory.Instance));
            Assert.That(settings.Host, Is.EqualTo("localhost"));
            Assert.That(settings.Database, Is.EqualTo("demo"));
            Assert.That(settings.ApplicationName, Is.EqualTo("extension"));
            Assert.That(url.Dialect, Is.TypeOf<PgsqlDialect>());
            Assert.That(url.Parametrizer, Is.TypeOf<PositionalParametrizer>());
        });
    }

    [Test]
    public void AlternativeConnector_ReusesFamilyMapperWithItsOwnSchemeAndFactory()
    {
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new NpgsqlConnectionStringBuilder());
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<PgsqlDialect>(["samplepg"]);
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(new ProviderRegistration(
            "Alternative PostgreSQL connector", ["samplepg"], "Sample.PostgreSql", () => factory.Object,
            dialects.Build().Get(typeof(PgsqlDialect)), () => new PositionalParametrizer(),
            context => new PostgresqlMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer)));
        var registry = builder.Build();
        var settings = new NpgsqlConnectionStringBuilder(new ConnectionUrl("samplepg://localhost/demo", registry).Parse());

        Assert.Multiple(() =>
        {
            Assert.That(settings.Database, Is.EqualTo("demo"));
            Assert.That(registry.GetMapper("samplepg"), Is.TypeOf<PostgresqlMapper>());
            Assert.That(registry.GetProviderFactory(["samplepg"]), Is.SameAs(factory.Object));
        });
    }

    [Test]
    public void DuplicateAlias_ReportsBothProviders()
    {
        var builder = CreateCatalog(() => null);
        var error = Assert.Throws<ProviderRegistrationException>(() => builder.AddProvider(new ProviderRegistration(
            "Conflicting connector", ["PG"], "Other.Data", () => null, CreateDialect(),
            () => new NamedParametrizer(), _ => throw new InvalidOperationException("Must not compile"))));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Alias, Is.EqualTo("PG"));
            Assert.That(error.ExistingRegistration, Is.EqualTo("PostgreSQL"));
            Assert.That(error.ConflictingRegistration, Is.EqualTo("Conflicting connector"));
        });
    }

    [Test]
    public void MissingFactory_SkipsSchemeAndReportsResolutionDiagnostic()
    {
        var registry = CreateCatalog(() => null).Build();
        var success = registry.TryResolve(new Parser().Parse("pg://localhost/demo"), out var resolved, out var diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionResolutionDiagnosticCode.SchemeNotFound));
            Assert.That(diagnostic.Message, Does.Contain("pg"));
        });
    }

    [Test]
    public void InvalidDatabasePath_ReportsProviderDiagnostic()
    {
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new NpgsqlConnectionStringBuilder());
        var registry = CreateCatalog(() => factory.Object).Build();
        var success = registry.TryResolve(new Parser().Parse("pg://localhost"), out _, out var diagnostic);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionResolutionDiagnosticCode.InvalidConnectionUri));
        });
    }

    [Test]
    public void Consumer_DoesNotReferenceOrLoadCompatibilityOrUnrelatedProviders()
    {
        var references = typeof(PostgresqlMapper).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(typeof(PostgresqlMapper).Assembly.GetExportedTypes(), Is.Not.Empty);
            Assert.That(references, Has.None.StartsWith("DubUrl.Provider."));
            Assert.That(loaded, Does.Not.Contain("DubUrl"));
            Assert.That(loaded, Does.Not.Contain("DubUrl.Providers"));
            Assert.That(loaded.Where(name => name!.StartsWith("DubUrl.Provider.", StringComparison.Ordinal)).Distinct(),
                Is.SubsetOf(new[] { "DubUrl.Provider.PostgreSql", "DubUrl.Provider.Testing" }));
        });
    }

    private static IDialect CreateDialect()
    {
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<PgsqlDialect>(["pg"]);
        return dialects.Build().Get(typeof(PgsqlDialect));
    }

    private static ProviderCatalogBuilder CreateCatalog(Func<DbProviderFactory?> factory)
    {
        var builder = new ProviderCatalogBuilder();
        builder.AddProvider(new ProviderRegistration(
            "PostgreSQL", ["pg"], "Npgsql", factory, CreateDialect(),
            () => new NamedParametrizer(),
            context => new PostgresqlMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer)));
        return builder;
    }

    private sealed class ParameterizedQuery : ICommandProvider, IParametrizedCommand
    {
        public ImmutableArray<DubUrlParameter> Parameters => [new DubUrlParameterInt("answer", 42)];
        public string Read(IDialect dialect, IConnectivity connectivity) => "SELECT @answer";
        public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;
    }
}
