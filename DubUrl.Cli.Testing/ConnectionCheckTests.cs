using System.Data.Common;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ConnectionCheckTests
{
    [TestCase("postgresql", "Public.Customer", "SELECT 1 FROM \"public\".\"customer\" WHERE 1=0")]
    [TestCase("oracle", "App.Customer", "SELECT 1 FROM \"APP\".\"CUSTOMER\" WHERE 1=0")]
    [TestCase("sqlserver", "dbo.[a]]b]", "SELECT 1 FROM [dbo].[a]]b] WHERE 1=0")]
    [TestCase("mysql", "app.customers", "SELECT 1 FROM `app`.`customers` WHERE 1=0")]
    [TestCase("postgresql", "\"Mixed.Case\"", "SELECT 1 FROM \"Mixed.Case\" WHERE 1=0")]
    public void QuotesAndFoldsNames(string family, string name, string expected)
        => Assert.That(ConnectionCheckSql.Table(family, name), Is.EqualTo(expected));

    [TestCase("customers; DROP TABLE customers")]
    [TestCase("customers WHERE 1=1")]
    [TestCase("a..b")]
    [TestCase("a.")]
    [TestCase("\"unterminated")]
    [TestCase("a/*comment*/")]
    public void RejectsExpressions(string name)
        => Assert.That(() => ConnectionCheckSql.Table("postgresql", name), Throws.ArgumentException);

    [TestCase(false)]
    [TestCase(true)]
    public async Task SqliteTableCheckSucceedsWithZeroRowsAndNeverChangesData(bool nonempty)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"check-{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SqliteConnection($"Data Source={path}"))
            {
                setup.Open();
                using var command = setup.CreateCommand();
                command.CommandText = "CREATE TABLE customer(id INTEGER)" + (nonempty ? "; INSERT INTO customer VALUES(1)" : "");
                command.ExecuteNonQuery();
            }
            var console = new RecordingConsole();
            DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
            await using var services = CliComposition.CreateDefault(console,
                providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
            var exit = await CliHost.InvokeAsync(["connection", "check", $"sqlite:///{path.Replace('\\', '/')}", "--table", "customer", "--output-format", "json"], services);
            using var json = JsonDocument.Parse(console.Output.ToString());
            Assert.That(exit, Is.Zero);
            Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(console.Errors.ToString(), Is.Empty);
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [Test]
    public async Task MissingSqliteFileIsNotCreated()
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"absent-{Guid.NewGuid():N}.db");
        var console = new RecordingConsole();
        DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
        var exit = await CliHost.InvokeAsync(["connection", "check", $"sqlite:///{path.Replace('\\', '/')}", "--output-format", "json"], services);
        Assert.That(exit, Is.EqualTo(5));
        Assert.That(File.Exists(path), Is.False);
    }

    [TestCase(false, "tableNotFound")]
    [TestCase(true, "executionFailed")]
    public async Task MissingViewDependencyDoesNotClaimRequestedViewIsAbsent(bool view, string expected)
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"resolution-{Guid.NewGuid():N}.db");
        try
        {
            using (var setup = new SqliteConnection($"Data Source={path}"))
            {
                setup.Open();
                if (view)
                {
                    using var command = setup.CreateCommand();
                    command.CommandText = "CREATE VIEW customer AS SELECT * FROM hidden";
                    command.ExecuteNonQuery();
                }
            }
            var console = new RecordingConsole();
            DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
            await using var services = CliComposition.CreateDefault(console,
                providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
            var exit = await CliHost.InvokeAsync(["connection", "check", $"sqlite:///{path.Replace('\\', '/')}", "--table", "customer", "--output-format", "json"], services);
            using var json = JsonDocument.Parse(console.Output.ToString());
            Assert.That(exit, Is.EqualTo(5));
            Assert.That(json.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo(expected));
        }
        finally { SqliteConnection.ClearAllPools(); File.Delete(path); }
    }

    [TestCase("--engine", "--table", "customer")]
    [TestCase("--timeout", "0")]
    [TestCase("--connection", "other")]
    [TestCase("--table", "customer;DROP TABLE customer")]
    public async Task InvalidInputsAreSanitized(params string[] arguments)
    {
        var console = new RecordingConsole();
        await using var services = CliComposition.CreateDefault(console);
        var exit = await CliHost.InvokeAsync(["connection", "check", "postgres://alice:sentinel@localhost/app", "--output-format", "json", .. arguments], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
    }

    private sealed class RecordingConsole : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader In => TextReader.Null;
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
    }
}
