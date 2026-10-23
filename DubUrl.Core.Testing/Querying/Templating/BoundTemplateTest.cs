using System.Data;
using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Provider.Oracle.Mapping;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using Microsoft.Data.SqlClient;
using Moq;
using Npgsql;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace DubUrl.Core.Testing.Querying.Templating;

public class BoundTemplateTest
{
    private sealed class FailingParametrizer : NamedParametrizer
    {
        public bool Fail { get; set; } = true;
        public override string GetPlaceholder(int position)
            => Fail && position == 2 ? throw new NotSupportedException("Test placeholder failure.") : base.GetPlaceholder(position);
    }

    private static InlineTemplateCommand Create(string source, IParametrizer parametrizer)
    {
        var dialect = Mock.Of<IDialect>(d => d.Renderer == new AnsiRenderer());
        return new InlineTemplateCommandFactory(dialect, parametrizer).Create(source);
    }

    private static IParametrizer Strategy(string kind) => kind switch
    {
        "named" => new NamedParametrizer(),
        "numbered" => new NpgsqlParametrizer(),
        _ => new PositionalParametrizer()
    };

    [TestCase("named", "@p1,@p2,@p3,@p4")]
    [TestCase("numbered", "$1,$2,$3,$4")]
    [TestCase("positional", "?,?,?,?")]
    public void Render_ConditionalLoopAndRepeatedOccurrences_PreservesEvaluationOrder(string kind, string expected)
    {
        var template = Create("$if(skip)$$ignored; format=\"parameter\"$$endif$$items:{x|$x; format=\"parameter\"$}; separator=\",\"$,$same; format=\"parameter\"$,$same; format=\"parameter\"$", Strategy(kind));
        var result = template.RenderCommand(new Dictionary<string, object?>
        {
            ["skip"] = false, ["ignored"] = 999, ["items"] = new[] { 10, 20 }, ["same"] = "O'Reilly"
        });
        Assert.Multiple(() =>
        {
            Assert.That(result.CommandText, Is.EqualTo(expected));
            Assert.That(result.Parameters.Select(p => p.Value), Is.EqualTo(new object[] { 10, 20, "O'Reilly", "O'Reilly" }));
            Assert.That(result.Parameters.Select(p => p.Name), Is.EqualTo(new[] { "p1", "p2", "p3", "p4" }));
        });
    }

    [Test]
    public void Render_NullsAndMetadata_BindsTypedNullAndSnapshotsExistingParameters()
    {
        var original = new DubUrlParameterDecimal("ignored", 12, 3, 123.456m);
        var template = Create("$a; format=\"parameter\"$,$b; format=\"parameter\"$,$c; format=\"parameter\"$,$d; format=\"parameter\"$", new NamedParametrizer());
        var result = template.RenderCommand(new Dictionary<string, object?>
        {
            ["a"] = null, ["b"] = new TemplateParameter(null, DbType.String, Size: 80),
            ["c"] = original, ["d"] = DBNull.Value
        });
        original.Value = 0;
        using var command = new SqlCommand();
        result.Bind(command);
        Assert.Multiple(() =>
        {
            Assert.That(command.Parameters.Count, Is.EqualTo(4));
            Assert.That(command.Parameters[0].Value, Is.EqualTo(DBNull.Value));
            Assert.That(command.Parameters[1].DbType, Is.EqualTo(DbType.String));
            Assert.That(command.Parameters[1].Size, Is.EqualTo(80));
            Assert.That(command.Parameters[1].Value, Is.EqualTo(DBNull.Value));
            Assert.That(command.Parameters[2].Value, Is.EqualTo(123.456m));
            Assert.That(command.Parameters[2].Precision, Is.EqualTo(12));
            Assert.That(command.Parameters[2].Scale, Is.EqualTo(3));
            Assert.That(command.Parameters[3].Value, Is.EqualTo(DBNull.Value));
        });
    }

    [Test]
    public async Task Render_ReuseAndConcurrentRenders_HaveIndependentState()
    {
        var template = Create("$if(include)$$value; format=\"parameter\"$$endif$", new NamedParametrizer());
        var results = await Task.WhenAll(Enumerable.Range(1, 20).Select(i => Task.Run(() =>
            template.RenderCommand(new Dictionary<string, object?> { ["include"] = true, ["value"] = i }))));
        var empty = template.RenderCommand(new Dictionary<string, object?> { ["include"] = false });
        Assert.Multiple(() =>
        {
            Assert.That(results.All(r => r.CommandText == "@p1" && r.Parameters.Length == 1), Is.True);
            Assert.That(results.Select(r => r.Parameters[0].Value), Is.EqualTo(Enumerable.Range(1, 20)));
            Assert.That(empty.Parameters, Is.Empty);
            Assert.That(empty.CommandText, Is.Empty);
        });
    }

    [Test]
    public void Render_LiteralsAndIdentifiers_RemainCompatible()
    {
        var template = Create("SELECT $x; format=\"value\"$ FROM $table; format=\"identity\"$", new NamedParametrizer());
        var values = new Dictionary<string, object?> { ["x"] = "O'Reilly", ["table"] = "a table" };
        Assert.Multiple(() =>
        {
            Assert.That(template.RenderCommand(values).CommandText, Is.EqualTo(template.Render(values)));
            Assert.That(template.RenderCommand(values).Parameters, Is.Empty);
        });
    }

    [Test]
    public void Render_StringOnlyApi_RejectsParameterFormat()
    {
        var template = Create("$x; format=\"parameter\"$", new NamedParametrizer());
        Assert.Throws<InvalidOperationException>(() => template.Render(new Dictionary<string, object?> { ["x"] = 1 }));
    }

    [Test]
    public void Render_LoopWithNullsAndBinary_BindsEachOccurrence()
    {
        var binary = new byte[] { 1, 2, 3 };
        var template = Create("$items:{x|$x; format=\"parameter\"$}; separator=\",\"$,$data; format=\"parameter\"$", new PositionalParametrizer());
        var result = template.RenderCommand(new Dictionary<string, object?>
        {
            ["items"] = new object?[] { 1, null, new TemplateParameter(null, DbType.Int32), 2 },
            ["data"] = binary
        });
        Assert.Multiple(() =>
        {
            Assert.That(result.CommandText, Is.EqualTo("?,?,?,?,?"));
            Assert.That(result.Parameters.Length, Is.EqualTo(5));
            Assert.That(result.Parameters[1].Value, Is.Null);
            Assert.That(result.Parameters[2].DbType, Is.EqualTo(DbType.Int32));
            Assert.That(result.Parameters[4].Value, Is.EqualTo(binary));
            Assert.That(result.Parameters[4].DbType, Is.EqualTo(DbType.Binary));
        });
    }

    [Test]
    public void Render_FailedRender_DoesNotRetainBindings()
    {
        var strategy = new FailingParametrizer();
        var template = Create("$x; format=\"parameter\"$,$x; format=\"parameter\"$", strategy);
        Assert.Throws<NotSupportedException>(() => template.RenderCommand(new Dictionary<string, object?> { ["x"] = 1 }));
        strategy.Fail = false;
        var next = template.RenderCommand(new Dictionary<string, object?> { ["x"] = 2 });
        Assert.That(next.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 2, 2 }));
        Assert.That(next.CommandText, Is.EqualTo("@p1,@p2"));
    }

    [Test]
    public void Provision_EmbeddedSubtemplate_RendersOncePerExecution()
    {
        var connection = new ConnectionUrl("pg://localhost/test");
        var dialect = connection.Dialect;
        var connectivity = connection.Connectivity;
        var resources = new Mock<IResourceTemplateManager>();
        resources.Setup(r => r.Any("query", dialect, connectivity.Alias)).Returns(true);
        resources.Setup(r => r.BestMatch("query", dialect, connectivity.Alias)).Returns("query.sql.st");
        resources.Setup(r => r.ReadResource("query.sql.st")).Returns("SELECT $part()$,$x; format=\"parameter\"$");
        resources.Setup(r => r.ListResources("parts", dialect.Aliases, connectivity.Alias, "sql.st"))
            .Returns(new Dictionary<string, string> { ["part"] = "parts.part.sql.st" });
        resources.Setup(r => r.ReadResource("parts.part.sql.st")).Returns("$x; format=\"parameter\"$");
        resources.Setup(r => r.ListResources("parts", dialect.Aliases, connectivity.Alias, "dic.st"))
            .Returns(new Dictionary<string, string>());
        var values = new Dictionary<string, object?> { ["x"] = 1 };
        var template = new EmbeddedTemplateCommand(resources.Object, "query", "parts", "parts", values, NullQueryLogger.Instance);
        var provisioners = new CommandProvisionerFactory().Instantiate(template, connection);
        using var first = new NpgsqlCommand();
        foreach (var provisioner in provisioners) provisioner.Execute(first);
        values["x"] = 2;
        using var second = new NpgsqlCommand();
        foreach (var provisioner in provisioners) provisioner.Execute(second);
        Assert.Multiple(() =>
        {
            Assert.That(first.CommandText, Is.EqualTo("SELECT $1,$2"));
            Assert.That(second.CommandText, Is.EqualTo(first.CommandText));
            Assert.That(first.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(second.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 2, 2 }));
        });
    }

    [Test]
    public void Provision_CrateDb_KeepsHandwrittenNamedBindingAndGeneratesPositionalBinding()
    {
        IParametrizer parametrizer = new NpgsqlNamedParametrizer();
        using var manual = new NpgsqlCommand();
        var parameter = new DubUrlParameterInt("original", 7);
        Assert.That(parametrizer.CreateParameter(manual, parameter).ParameterName, Is.EqualTo("original"));
        using var generated = new NpgsqlCommand();
        var result = Create("$x; format=\"parameter\"$", parametrizer)
            .RenderCommand(new Dictionary<string, object?> { ["x"] = 7 });
        result.Bind(generated);
        Assert.That(generated.CommandText, Is.EqualTo("$1"));
        Assert.That(generated.Parameters[0].ParameterName, Is.Empty);
    }

    [TestCase("mssql", "SELECT @p1,@p2", "p1")]
    [TestCase("pg", "SELECT $1,$2", "")]
    [TestCase("oracle", "SELECT :p1,:p2", "p1")]
    [TestCase("odbc+mssql", "SELECT ?,?", "Parameter1")]
    public void Provision_ConnectorRegistration_UsesMatchingPlaceholderAndBinding(string scheme, string sql, string name)
    {
        var connection = new ConnectionUrl($"{scheme}://localhost/test");
        using DbCommand command = scheme switch
        {
            "pg" => new NpgsqlCommand(), "oracle" => new OracleCommand(),
            "odbc+mssql" => new System.Data.Odbc.OdbcCommand(), _ => new SqlCommand()
        };
        var template = new InlineTemplateCommandFactory(connection.Dialect, connection.Parametrizer)
            .Create("SELECT $value; format=\"parameter\"$,$value; format=\"parameter\"$");
        var result = template.RenderCommand(new Dictionary<string, object?> { ["value"] = 42 });
        foreach (var provisioner in new CommandProvisionerFactory().Instantiate(result, connection))
            provisioner.Execute(command);
        Assert.Multiple(() =>
        {
            Assert.That(command.CommandText, Is.EqualTo(sql));
            Assert.That(command.Parameters.Count, Is.EqualTo(2));
            Assert.That(command.Parameters[0].ParameterName, Is.EqualTo(name));
            Assert.That(command.Parameters.Cast<DbParameter>().Select(p => p.Value), Is.EqualTo(new[] { 42, 42 }));
            if (command is OracleCommand oracle) Assert.That(oracle.BindByName, Is.True);
        });
    }

    [Test]
    public async Task Execute_DatabaseUrl_SyncAndAsyncUseBoundCommands()
    {
        using var database = new DatabaseUrl("duckdb://memory");
        var template = database.CreateTemplate("SELECT $value; format=\"parameter\"$ + $value; format=\"parameter\"$");
        var values = new Dictionary<string, object?> { ["value"] = 21 };
        Assert.That(database.ReadScalar<int>(template, values), Is.EqualTo(42));
        values["value"] = 4;
        Assert.That(await database.ReadScalarAsync<int>(template, values), Is.EqualTo(8));
    }
}
