using System.Data;
using System.Data.Common;
using System.Reflection;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using Microsoft.Data.SqlClient;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Querying.Templating;

public class TypedTemplateTest
{
    private sealed class Sql<T>(string sql) : TypedCommandTemplate<T>
    {
        public override string TemplateText => sql;
    }

    private sealed record Inputs(string Table, int Id, Optional<string?> Name = default);
    private sealed record NonNull(string Required, Optional<string> Optional);
    private sealed record Values(object? Value);
    private sealed record ListInput(int[] Items);

    private static RenderedTemplateCommand Render<T>(string sql, T values, IParametrizer? parametrizer = null)
    {
        var provider = new Sql<T>(sql).Bind(values);
        return ((TypedTemplateCommand)provider).RenderCommand(Mock.Of<IDialect>(d => d.Renderer == new AnsiRenderer()), parametrizer ?? new NamedParametrizer());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Render_MatchesDictionaryAndPreservesSqlInjectionSafety(bool positional)
    {
        const string sql = "SELECT * FROM $Table; format=\"identity\"$ WHERE Id=$Id; format=\"parameter\"$ OR Parent=$Id; format=\"parameter\"$";
        IParametrizer parametrizer = positional ? new PositionalParametrizer() : new NamedParametrizer();
        var input = new Inputs("a\"; DROP TABLE Customer;--", 42);
        var actual = Render(sql, input, parametrizer);
        var expected = new InlineTemplateCommandFactory(Mock.Of<IDialect>(d => d.Renderer == new AnsiRenderer()), parametrizer)
            .Create(sql).RenderCommand(new Dictionary<string, object?> { ["Table"] = input.Table, ["Id"] = 42 });
        Assert.That(actual.CommandText, Is.EqualTo(expected.CommandText));
        Assert.That(actual.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 42, 42 }));
        const string attack = "'; DROP TABLE Customer;--";
        var bound = Render("SELECT $Value; format=\"parameter\"$", new Values(attack));
        Assert.That(bound.CommandText, Does.Not.Contain(attack));
        Assert.That(bound.Parameters[0].Value, Is.EqualTo(attack));
    }

    [TestCase("$Missing; format=\"parameter\"$")]
    [TestCase("$id; format=\"parameter\"$")]
    [TestCase("$if(Name.HasValue)$$Missing; format=\"parameter\"$$endif$")]
    [TestCase("$if(Name.hasValue)$$Name.Value; format=\"parameter\"$$endif$")]
    [TestCase("$if(Name)$$Id; format=\"parameter\"$$endif$")]
    [TestCase("$Id; separator=Missing$")]
    [TestCase("$if(Id)$incomplete")]
    [TestCase("$first(Missing); format=\"parameter\"$")]
    [TestCase("$i$")]
    public void Render_InvalidContract_FailsEvenInDisabledBranches(string sql)
        => Assert.Throws<ArgumentException>(() => Render(sql, new Inputs("Customer", 1)));

    [TestCase(false)]
    [TestCase(true)]
    public void Render_AbsentAndPresentNull_HaveDifferentPresenceAndBindings(bool positional)
    {
        const string sql = "SELECT $Id; format=\"parameter\"$$if(Name.HasValue)$,$Name.Value; format=\"parameter\"$$endif$";
        IParametrizer strategy = positional ? new PositionalParametrizer() : new NamedParametrizer();
        var absent = Render(sql, new Inputs("Customer", 0), strategy);
        var present = Render(sql, new Inputs("Customer", 0, Optional<string?>.Some(null)), strategy);
        Assert.That(absent.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 0 }));
        Assert.That(present.Parameters.Length, Is.EqualTo(2));
        Assert.That(present.Parameters[1].Value, Is.Null);
        Assert.That(absent.CommandText, Is.EqualTo(positional ? "SELECT ?" : "SELECT @p1"));
        Assert.That(present.CommandText, Is.EqualTo(positional ? "SELECT ?,?" : "SELECT @p1,@p2"));
    }

    [TestCase("parameter")]
    [TestCase("value")]
    [TestCase("identity")]
    public void Render_ActiveAbsentInput_ThrowsClearError(string format)
    {
        var exception = Assert.Throws<ArgumentException>(() => Render($"SELECT $Name.Value; format=\"{format}\"$", new Inputs("Customer", 1)));
        Assert.That(exception!.Message, Does.Contain("Name").And.Contain("active"));
    }

    [Test]
    public void Render_NonNullContracts_RejectPresentNull()
    {
        Assert.Throws<ArgumentException>(() => Render("$Required$", new NonNull(null!, default)));
        Assert.Throws<ArgumentException>(() => Render("$Required$", new NonNull("ok", Optional<string>.Some(null!))));
        Assert.That(Render("$Required$", new NonNull("ok", default)).CommandText, Is.EqualTo("ok"));
        Assert.Throws<InvalidOperationException>(() => _ = Optional<int>.None.Value);
    }

    private sealed record OptionalValues(Optional<int> Number, Optional<bool> Flag, Optional<string> Text);

    [Test]
    public void Render_ZeroFalseAndEmptyString_ArePresent()
    {
        const string sql = "$if(Number.HasValue)$$Number.Value; format=\"parameter\"$$endif$,$if(Flag.HasValue)$$Flag.Value; format=\"parameter\"$$endif$,$if(Text.HasValue)$$Text.Value; format=\"parameter\"$$endif$";
        var result = Render(sql, new OptionalValues(new(0), new(false), new("")));
        Assert.That(result.Parameters.Select(p => p.Value), Is.EqualTo(new object[] { 0, false, "" }));
    }

    [Test]
    public void Render_LoopLocalsAndMetadata_UseExistingEngine()
    {
        var loop = Render("$Items:{x|$x; format=\"parameter\"$}; separator=\",\"$", new ListInput([1, 2]), new PositionalParametrizer());
        Assert.That(loop.CommandText, Is.EqualTo("?,?"));
        Assert.That(loop.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 1, 2 }));
        var typed = Render("$Value; format=\"parameter\"$", new Values(new TemplateParameter(null, DbType.String, Size: 80)));
        using var command = new SqlCommand();
        typed.Bind(command);
        Assert.That(command.Parameters[0].Value, Is.EqualTo(DBNull.Value));
        Assert.That(command.Parameters[0].Size, Is.EqualTo(80));
        Assert.That(command.Parameters[0].DbType, Is.EqualTo(DbType.String));
    }

    private sealed record NullableInputs(int? Required, Optional<int?> Optional, Optional<TemplateParameter> Typed);

    [Test]
    public void Render_BuiltInFunctionsAndLoopIndices_AreNotContractProperties()
    {
        var result = Render("$if(length(Items))$$first(Items); format=\"parameter\"$,$Items:{x|$i; format=\"parameter\"$}; separator=\",\"$$endif$", new ListInput([10, 20]));
        Assert.That(result.Parameters.Select(p => p.Value), Is.EqualTo(new[] { 10, 1, 2 }));
    }

    [Test]
    public void Render_NullableValueTypesAndOptionalMetadata_PreserveNullAndType()
    {
        var result = Render("$Required; format=\"parameter\"$,$Optional.Value; format=\"parameter\"$,$Typed.Value; format=\"parameter\"$",
            new NullableInputs(null, new(null), new(new TemplateParameter(null, DbType.Int32))));
        Assert.That(result.Parameters.Select(p => p.Value), Is.EqualTo(new object?[] { null, null, null }));
        Assert.That(result.Parameters[2].DbType, Is.EqualTo(DbType.Int32));
        var bytes = Render("$Value; format=\"parameter\"$", new Values(new byte[] { 1, 2 }));
        Assert.That(bytes.Parameters.Single().DbType, Is.EqualTo(DbType.Binary));
    }

    [Test]
    public async Task Render_ConcurrentReuseAndFailure_DoNotRetainInputState()
    {
        var template = new Sql<Inputs>("$Id; format=\"parameter\"$$if(Name.HasValue)$,$Name.Value; format=\"parameter\"$$endif$");
        using var database = new DatabaseUrl("duckdb://memory");
        var commands = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(() => template.RenderCommand(database, new Inputs("Customer", i)))));
        Assert.That(commands.Select(c => c.Parameters.Single().Value), Is.EqualTo(Enumerable.Range(0, 30)));
        Assert.That(commands.All(c => c.CommandText == "?"), Is.True);
        Assert.Throws<ArgumentException>(() => template.RenderCommand(database, new Inputs(null!, 1)));
        Assert.That(template.RenderCommand(database, new Inputs("Customer", 31)).Parameters.Single().Value, Is.EqualTo(31));
    }

    public sealed class Row { public int Value { get; set; } }
    private static readonly Sql<Values> Query = new("SELECT CAST($Value; format=\"parameter\"$ AS INTEGER) AS Value");

    [Test]
    public async Task Execute_AllBaseQueryVariants_ThroughConcreteAndInterfaces()
    {
        using var database = new DatabaseUrl("duckdb://memory");
        IDatabaseUrl sync = database;
        IAsyncDatabaseUrl asyncDatabase = database;
        var input = new Values(42);
        Assert.That(sync.ReadScalar(Query, input), Is.EqualTo(42));
        Assert.That(sync.ReadScalar<int, Values>(Query, input), Is.EqualTo(42));
        Assert.That(sync.ReadScalarNonNull(Query, input), Is.EqualTo(42));
        Assert.That(sync.ReadScalarNonNull<int, Values>(Query, input), Is.EqualTo(42));
        Assert.That(database.ReadScalar<int>(Query.RenderCommand(database, input)), Is.EqualTo(42));
        Assert.That(sync.ReadSingle(Query, input), Is.Not.Null);
        Assert.That(sync.ReadSingleNonNull(Query, input), Is.Not.Null);
        Assert.That(sync.ReadFirst(Query, input), Is.Not.Null);
        Assert.That(sync.ReadFirstNonNull(Query, input), Is.Not.Null);
        Assert.That(sync.ReadMultiple(Query, input).Count(), Is.EqualTo(1));
        using (var reader = sync.ExecuteReader(Query, input))
        {
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.GetInt32(0), Is.EqualTo(42));
        }
        Assert.That(await asyncDatabase.ReadScalarAsync(Query, input), Is.EqualTo(42));
        Assert.That(await asyncDatabase.ReadScalarAsync<int, Values>(Query, input), Is.EqualTo(42));
        Assert.That(await asyncDatabase.ReadScalarNonNullAsync(Query, input), Is.EqualTo(42));
        Assert.That(await asyncDatabase.ReadScalarNonNullAsync<int, Values>(Query, input), Is.EqualTo(42));
        Assert.That(await asyncDatabase.ReadSingleAsync(Query, input), Is.Not.Null);
        Assert.That(await asyncDatabase.ReadSingleNonNullAsync(Query, input), Is.Not.Null);
        Assert.That(await asyncDatabase.ReadFirstAsync(Query, input), Is.Not.Null);
        Assert.That(await asyncDatabase.ReadFirstNonNullAsync(Query, input), Is.Not.Null);
        var rows = 0;
        await foreach (var row in asyncDatabase.ReadMultipleAsync(Query, input)) rows++;
        Assert.That(rows, Is.EqualTo(1));
        await using var asyncReader = await asyncDatabase.ExecuteReaderAsync(Query, input);
        Assert.That(await asyncReader.ReadAsync(), Is.True);
        Assert.That(asyncReader.GetInt32(0), Is.EqualTo(42));
    }

    [Test]
    public async Task Execute_AllMicroOrmQueryVariants_MapRows()
    {
        await using var source = new ConnectionSourceUrl("duckdb://memory").Create();
        await using var database = new DubUrl.MicroOrm.DatabaseUrl(source);
        var input = new Values(42);
        Assert.That(database.ReadSingle<Row, Values>(Query, input)!.Value, Is.EqualTo(42));
        Assert.That(database.ReadSingleNonNull<Row, Values>(Query, input)!.Value, Is.EqualTo(42));
        Assert.That(database.ReadFirst<Row, Values>(Query, input)!.Value, Is.EqualTo(42));
        Assert.That(database.ReadFirstNonNull<Row, Values>(Query, input).Value, Is.EqualTo(42));
        Assert.That(database.ReadMultiple<Row, Values>(Query, input).Single().Value, Is.EqualTo(42));
        Assert.That((await database.ReadSingleAsync<Row, Values>(Query, input))!.Value, Is.EqualTo(42));
        Assert.That((await database.ReadSingleNonNullAsync<Row, Values>(Query, input)).Value, Is.EqualTo(42));
        Assert.That((await database.ReadFirstAsync<Row, Values>(Query, input))!.Value, Is.EqualTo(42));
        Assert.That((await database.ReadFirstNonNullAsync<Row, Values>(Query, input)).Value, Is.EqualTo(42));
        await foreach (var row in database.ReadMultipleAsync<Row, Values>(Query, input)) Assert.That(row.Value, Is.EqualTo(42));
    }

    private sealed record OptionalNumber(Optional<int?> Value);

    [Test]
    public async Task Execute_OptionalAbsenceNullAndValue_AreDistinct()
    {
        using var database = new DatabaseUrl("duckdb://memory");
        var template = new Sql<OptionalNumber>("SELECT CAST($if(Value.HasValue)$$Value.Value; format=\"parameter\"$$else$7$endif$ AS INTEGER)");
        Assert.That(database.ReadScalar<int?, OptionalNumber>(template, new(default)), Is.EqualTo(7));
        Assert.That(database.ReadScalar<int?, OptionalNumber>(template, new(new(null))), Is.Null);
        Assert.That(database.ReadScalar<int?, OptionalNumber>(template, new(new(0))), Is.Zero);
        Assert.That(await database.ReadScalarAsync<int?, OptionalNumber>(template, new(default)), Is.EqualTo(7));
        Assert.That(await database.ReadScalarAsync<int?, OptionalNumber>(template, new(new(null))), Is.Null);
        Assert.That(await database.ReadScalarAsync<int?, OptionalNumber>(template, new(new(0))), Is.Zero);
    }

    [Test]
    public async Task Execute_EmptyAndMultipleRows_PreserveCardinalityAndNullRules()
    {
        using var database = new DatabaseUrl("duckdb://memory");
        var empty = new Sql<Values>(Query.TemplateText + " WHERE FALSE");
        var multiple = new Sql<Values>(Query.TemplateText + " UNION ALL SELECT 99 AS Value");
        var input = new Values(42);
        Assert.That(database.ReadSingle(empty, input), Is.Null);
        Assert.That(await database.ReadFirstAsync(empty, input), Is.Null);
        Assert.Throws<InvalidOperationException>(() => database.ReadSingleNonNull(empty, input));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await database.ReadFirstNonNullAsync(empty, input));
        Assert.Throws<InvalidOperationException>(() => database.ReadSingle(multiple, input));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await database.ReadSingleAsync(multiple, input));
        Assert.That(((IDictionary<string, object?>)database.ReadFirst(multiple, input)!)["Value"], Is.EqualTo(42));
        Assert.That(database.ReadScalar<int?, Values>(Query, new(null)), Is.Null);
        Assert.Throws<InvalidOperationException>(() => database.ReadScalarNonNull(Query, new(null)));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await database.ReadScalarNonNullAsync(Query, new(null)));
    }

    [Test]
    public void Render_StringOnlyTemplates_KeepExistingLiteralSemantics()
    {
        using var database = new DatabaseUrl("duckdb://memory");
        var template = new Sql<Inputs>("SELECT $Id; format=\"value\"$ FROM $Table; format=\"identity\"$");
        var input = new Inputs("Customer", 42);
        Assert.That(template.Render(database, input), Is.EqualTo(template.RenderCommand(database, input).CommandText));
    }

    [Test]
    public async Task Execute_StreamsAndReaders_HoldLeaseUntilDisposedAndFailuresReleaseIt()
    {
        await using var source = new ConnectionSourceUrl("duckdb://memory").Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        await using var database = new DatabaseUrl(source);
        var input = new Values(42);
        var rows = database.ReadMultiple(Query, input);
        using (var lease = source.Acquire()) { } // Enumeration is still lazy.
        using (var iterator = rows.GetEnumerator())
        {
            Assert.That(iterator.MoveNext(), Is.True);
            using var cancellation = new CancellationTokenSource();
            var waiting = source.AcquireAsync(cancellation.Token).AsTask();
            Assert.That(waiting.IsCompleted, Is.False);
            cancellation.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await waiting);
        }
        await using (var iterator = database.ReadMultipleAsync(Query, input).GetAsyncEnumerator())
            Assert.That(await iterator.MoveNextAsync(), Is.True);
        using (var reader = database.ExecuteReader(Query, input)) Assert.That(reader.Read(), Is.True);
        await using (var reader = await database.ExecuteReaderAsync(Query, input)) Assert.That(await reader.ReadAsync(), Is.True);
        var bad = new Sql<Values>("SELECT * FROM table_that_does_not_exist WHERE x=$Value; format=\"parameter\"$");
        var absent = new Sql<Inputs>("SELECT $Name.Value; format=\"parameter\"$");
        Assert.Throws<ArgumentException>(() => database.ReadScalar(absent, new Inputs("Customer", 42)));
        Assert.Catch<DbException>(() => database.ReadSingle(bad, input));
        await Assert.CatchAsync<DbException>(async () => await database.ReadSingleAsync(bad, input));
        Assert.That(database.ReadScalar(Query, input), Is.EqualTo(42));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await database.ReadScalarAsync(Query, input, cancelled.Token));
        await using var finalLease = await source.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase(typeof(DatabaseUrl))]
    [TestCase(typeof(IDatabaseUrl))]
    [TestCase(typeof(IAsyncDatabaseUrl))]
    [TestCase(typeof(DubUrl.MicroOrm.DatabaseUrl))]
    public void ApiCoverage_EveryCommandProviderQueryHasTypedOverload(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var queries = methods.Where(m => m.GetParameters().FirstOrDefault()?.ParameterType == typeof(ICommandProvider));
        foreach (var method in queries)
        {
            var arity = method.GetGenericArguments().Length;
            Assert.That(methods.Any(candidate => candidate.Name == method.Name
                && candidate.GetGenericArguments().Length == arity + 1
                && candidate.GetParameters().FirstOrDefault()?.ParameterType.IsGenericType == true
                && candidate.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(TypedCommandTemplate<>)),
                Is.True, $"Missing typed overload for {type.Name}.{method}");
        }
    }
}
