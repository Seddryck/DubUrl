using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DubUrl;

public partial class DatabaseUrl : IAsyncDatabaseUrl
{
    protected ConnectionUrl ConnectionUrl { get; }
    protected CommandProvisionerFactory CommandProvisionerFactory { get; }
    protected readonly InlineTemplateCommandFactory _inlineTemplateCommandFactory;
    private readonly DatabaseCommandExecutor _executor;
    private readonly object _lifetimeSync = new();
    private ConnectionSource? _source;
    private readonly bool _ownsSource = true;
    private bool _disposed;

    protected ICaster[] Casters { get; } = [];

    public IQueryLogger QueryLogger { get; }

    public IDialect Dialect { get => ConnectionUrl.Dialect; }

    public DatabaseUrl(string url)
        : this(new ConnectionUrlFactory(SchemeRegistryBuilder.GetDefault()).Instantiate(url), new(), NullQueryLogger.Instance)
    { }

    public DatabaseUrl(ConnectionUrlFactory factory, string url)
        : this(factory.Instantiate(url), new(), NullQueryLogger.Instance)
    { }

    internal DatabaseUrl(ConnectionUrlFactory factory, CommandProvisionerFactory commandProvisionerFactory, string url)
        : this(factory.Instantiate(url), commandProvisionerFactory, NullQueryLogger.Instance)
    { }

    public DatabaseUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IQueryLogger logger)
    {
        (ConnectionUrl, CommandProvisionerFactory, QueryLogger, _inlineTemplateCommandFactory) =
            (connectionUrl, commandProvisionerFactory, logger, new InlineTemplateCommandFactory(connectionUrl.Dialect));
        _executor = new DatabaseCommandExecutor(connectionUrl, commandProvisionerFactory, logger, GetSource);
    }

    /// <summary>Uses a borrowed source with its bound dialect and parameterization context.</summary>
    public DatabaseUrl(ConnectionSource source)
        : this(source, new CommandProvisionerFactory(), NullQueryLogger.Instance) { }

    public DatabaseUrl(ConnectionSource source, CommandProvisionerFactory commandProvisionerFactory, IQueryLogger logger)
        : this(source?.QueryContext ?? throw new ArgumentNullException(nameof(source)), commandProvisionerFactory, logger)
        => (_source, _ownsSource) = (source, false);

    private ConnectionSource GetSource()
    {
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Existing constructors preserve per-operation connection ownership, independently of source defaults.
            return _source ??= new ConnectionSource(ConnectionUrl, ConnectionReuseMode.ProviderManaged);
        }
    }

    public void Dispose()
    {
        TakeOwnedSource()?.Dispose();
    }

    public ValueTask DisposeAsync() => TakeOwnedSource()?.DisposeAsync() ?? default;

    private ConnectionSource? TakeOwnedSource()
    {
        ConnectionSource? owned;
        lock (_lifetimeSync)
        {
            _disposed = true;
            owned = _ownsSource ? _source : null;
        }
        return owned;
    }

    protected virtual IDbCommand PrepareCommand(ICommandProvider commandProvider)
        => _executor.PrepareOwnedCommand(commandProvider);

    protected T ExecuteReader<T>(ICommandProvider commandProvider, Func<IDataReader, T> consume)
        => _executor.ExecuteReader(commandProvider, consume);

    protected Task<T> ExecuteReaderAsync<T>(ICommandProvider commandProvider, Func<DbDataReader, CancellationToken, Task<T>> consume, CancellationToken cancellationToken)
        => _executor.ExecuteReaderAsync(commandProvider, consume, cancellationToken);

    protected IEnumerable<T> Stream<T>(ICommandProvider commandProvider, Func<IDataReader, T> project)
        => _executor.Stream(commandProvider, project);

    protected IAsyncEnumerable<T> StreamAsync<T>(ICommandProvider commandProvider, Func<DbDataReader, T> project, CancellationToken cancellationToken)
        => _executor.StreamAsync(commandProvider, project, cancellationToken);

    public InlineTemplateCommand CreateTemplate(string source)
        => _inlineTemplateCommandFactory.Create(source);

    #region Scalar

    public object? ReadScalar(string query)
        => ReadScalar(new InlineCommand(query, QueryLogger));

    public object? ReadScalar(string template, IDictionary<string, object?> parameters)
       => ReadScalar(CreateTemplate(template), parameters);

    public object? ReadScalar(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadScalar(template.Render(parameters));

    public object? ReadScalar(ICommandProvider commandProvider)
        => _executor.ExecuteScalar(commandProvider, command => command.ExecuteScalar());

    public T? ReadScalar<T>(string query)
      => ReadScalar<T>(new InlineCommand(query, QueryLogger));

    public T? ReadScalar<T>(string template, IDictionary<string, object?> parameters)
       => ReadScalar<T>(CreateTemplate(template), parameters);

    public T? ReadScalar<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadScalar<T>(template.Render(parameters));

    public T? ReadScalar<T>(ICommandProvider query)
    {
        var result = ReadScalar(query);
        return (T?)(result == DBNull.Value ? null : result);
    }

    public object ReadScalarNonNull(string query)
        => ReadScalarNonNull(new InlineCommand(query, QueryLogger));

    public object ReadScalarNonNull(string template, IDictionary<string, object?> parameters)
       => ReadScalarNonNull(CreateTemplate(template), parameters);

    public object ReadScalarNonNull(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadScalarNonNull(template.Render(parameters));

    public object ReadScalarNonNull(ICommandProvider commandProvider)
    {
        var result = ReadScalar(commandProvider);
        var typedResult = result == DBNull.Value ? null : result;
        return typedResult ?? throw new InvalidOperationException("The scalar query returned null.");
    }

    public T ReadScalarNonNull<T>(string query)
       => ReadScalarNonNull<T>(new InlineCommand(query, QueryLogger));

    public T ReadScalarNonNull<T>(string template, IDictionary<string, object?> parameters)
       => ReadScalarNonNull<T>(CreateTemplate(template), parameters);

    public T ReadScalarNonNull<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadScalarNonNull<T>(template.Render(parameters));

    public T ReadScalarNonNull<T>(ICommandProvider query)
    {
        var result = ReadScalarNonNull(query);
        if (result is T t)
            return t;

        return NormalizeReturnType<T>(result) ?? throw new InvalidOperationException("The scalar caster returned null.");
    }

    public Task<object?> ReadScalarAsync(string query, CancellationToken cancellationToken = default)
        => ReadScalarAsync(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<object?> ReadScalarAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object?> ReadScalarAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync(template.Render(parameters), cancellationToken);

    public Task<object?> ReadScalarAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => _executor.ExecuteScalarAsync(commandProvider, (command, token) => command.ExecuteScalarAsync(token), cancellationToken);

    public async Task<T?> ReadScalarAsync<T>(string query, CancellationToken cancellationToken = default)
        => await ReadScalarAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public Task<T?> ReadScalarAsync<T>(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync<T>(CreateTemplate(template), parameters, cancellationToken);

    public Task<T?> ReadScalarAsync<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync<T>(template.Render(parameters), cancellationToken);

    public async Task<T?> ReadScalarAsync<T>(ICommandProvider query, CancellationToken cancellationToken = default)
    {
        var result = await ReadScalarAsync(query, cancellationToken).ConfigureAwait(false);
        return (T?)(result == DBNull.Value ? null : result);
    }

    public async Task<object> ReadScalarNonNullAsync(string query, CancellationToken cancellationToken = default)
        => await ReadScalarNonNullAsync(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public Task<object> ReadScalarNonNullAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object> ReadScalarNonNullAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync(template.Render(parameters), cancellationToken);

    public async Task<object> ReadScalarNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
    {
        var result = await ReadScalarAsync(commandProvider, cancellationToken).ConfigureAwait(false);
        var typedResult = result == DBNull.Value ? null : result;
        return typedResult ?? throw new InvalidOperationException("The scalar query returned null.");
    }

    public async Task<T> ReadScalarNonNullAsync<T>(string query, CancellationToken cancellationToken = default)
        => await ReadScalarNonNullAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public Task<T> ReadScalarNonNullAsync<T>(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync<T>(CreateTemplate(template), parameters, cancellationToken);

    public Task<T> ReadScalarNonNullAsync<T>(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync<T>(template.Render(parameters), cancellationToken);

    public async Task<T> ReadScalarNonNullAsync<T>(ICommandProvider query, CancellationToken cancellationToken = default)
    {
        var result = await ReadScalarNonNullAsync(query, cancellationToken).ConfigureAwait(false);
        if (result is T typed)
            return typed;

        return NormalizeReturnType<T>(result) ?? throw new InvalidOperationException("The scalar caster returned null.");
    }

    #endregion

    #region Single

    public object? ReadSingle(string query)
        => ReadSingle(new InlineCommand(query, QueryLogger));

    public object? ReadSingle(string template, IDictionary<string, object?> parameters)
       => ReadSingle(CreateTemplate(template), parameters);

    public object? ReadSingle(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadSingle(template.Render(parameters));

    public object? ReadSingle(ICommandProvider commandProvider)
        => ExecuteReader(commandProvider, reader =>
        {
            if (!reader.Read())
                return null;
            var result = reader.ToExpandoObject();
            return !reader.Read() ? result : throw new InvalidOperationException();
        });

    public Task<object?> ReadSingleAsync(string query, CancellationToken cancellationToken = default)
        => ReadSingleAsync(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<object?> ReadSingleAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadSingleAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object?> ReadSingleAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadSingleAsync(template.Render(parameters), cancellationToken);

    public Task<object?> ReadSingleAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(commandProvider, async (reader, token) =>
        {
            if (!await reader.ReadAsync(token).ConfigureAwait(false))
                return null;
            var result = reader.ToExpandoObject();
            return !await reader.ReadAsync(token).ConfigureAwait(false) ? result : throw new InvalidOperationException();
        }, cancellationToken);

    public object ReadSingleNonNull(string query)
        => ReadSingleNonNull(new InlineCommand(query, QueryLogger));

    public object ReadSingleNonNull(string template, IDictionary<string, object?> parameters)
       => ReadSingleNonNull(CreateTemplate(template), parameters);

    public object ReadSingleNonNull(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadSingleNonNull(template.Render(parameters));

    public object ReadSingleNonNull(ICommandProvider commandProvider)
        => ReadSingle(commandProvider) ?? throw new InvalidOperationException();

    public async Task<object> ReadSingleNonNullAsync(string query, CancellationToken cancellationToken = default)
        => await ReadSingleNonNullAsync(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public Task<object> ReadSingleNonNullAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadSingleNonNullAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object> ReadSingleNonNullAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadSingleNonNullAsync(template.Render(parameters), cancellationToken);

    public async Task<object> ReadSingleNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => await ReadSingleAsync(commandProvider, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException();

    #endregion

    #region First 

    public object? ReadFirst(string query)
        => ReadFirst(new InlineCommand(query, QueryLogger));

    public object? ReadFirst(string template, IDictionary<string, object?> parameters)
       => ReadFirst(CreateTemplate(template), parameters);

    public object? ReadFirst(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadFirst(template.Render(parameters));

    public object? ReadFirst(ICommandProvider commandProvider)
        => ExecuteReader(commandProvider, reader => reader.Read() ? reader.ToExpandoObject() : null);

    public Task<object?> ReadFirstAsync(string query, CancellationToken cancellationToken = default)
        => ReadFirstAsync(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<object?> ReadFirstAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadFirstAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object?> ReadFirstAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadFirstAsync(template.Render(parameters), cancellationToken);

    public Task<object?> ReadFirstAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(commandProvider, async (reader, token) =>
            await reader.ReadAsync(token).ConfigureAwait(false) ? reader.ToExpandoObject() : null, cancellationToken);

    public object ReadFirstNonNull(string query)
        => ReadFirstNonNull(new InlineCommand(query, QueryLogger));

    public object ReadFirstNonNull(string template, IDictionary<string, object?> parameters)
       => ReadFirstNonNull(CreateTemplate(template), parameters);

    public object ReadFirstNonNull(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadFirstNonNull(template.Render(parameters));

    public object ReadFirstNonNull(ICommandProvider commandProvider)
        => ReadFirst(commandProvider) ?? throw new InvalidOperationException();

    public async Task<object> ReadFirstNonNullAsync(string query, CancellationToken cancellationToken = default)
        => await ReadFirstNonNullAsync(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public Task<object> ReadFirstNonNullAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadFirstNonNullAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<object> ReadFirstNonNullAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadFirstNonNullAsync(template.Render(parameters), cancellationToken);

    public async Task<object> ReadFirstNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => await ReadFirstAsync(commandProvider, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException();

    #endregion

    #region Multiple

    public IEnumerable<object> ReadMultiple(string query)
        => ReadMultiple(new InlineCommand(query, QueryLogger));

    public IEnumerable<object> ReadMultiple(string template, IDictionary<string, object?> parameters)
       => ReadMultiple(CreateTemplate(template), parameters);

    public IEnumerable<object> ReadMultiple(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ReadMultiple(template.Render(parameters));
    
    public IEnumerable<object> ReadMultiple(ICommandProvider commandProvider)
        => Stream(commandProvider, reader => reader.ToExpandoObject());

    public IAsyncEnumerable<object> ReadMultipleAsync(string query, CancellationToken cancellationToken = default)
        => ReadMultipleAsync(new InlineCommand(query, QueryLogger), cancellationToken);

    public IAsyncEnumerable<object> ReadMultipleAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadMultipleAsync(CreateTemplate(template), parameters, cancellationToken);

    public IAsyncEnumerable<object> ReadMultipleAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ReadMultipleAsync(template.Render(parameters), cancellationToken);

    public IAsyncEnumerable<object> ReadMultipleAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => StreamAsync(commandProvider, reader => reader.ToExpandoObject(), cancellationToken);

    #endregion

    #region ExecuteReader

    public OwnedDataReader ExecuteReader(string query)
       => ExecuteReader(new InlineCommand(query, QueryLogger));

    public OwnedDataReader ExecuteReader(string template, IDictionary<string, object?> parameters)
       => ExecuteReader(CreateTemplate(template), parameters);

    public OwnedDataReader ExecuteReader(InlineTemplateCommand template, IDictionary<string, object?> parameters)
       => ExecuteReader(template.Render(parameters));

    public OwnedDataReader ExecuteReader(ICommandProvider commandProvider)
        => _executor.ExecuteOwnedReader(commandProvider);

    IDataReader IDatabaseUrl.ExecuteReader(string query) => ExecuteReader(query);

    IDataReader IDatabaseUrl.ExecuteReader(ICommandProvider commandProvider) => ExecuteReader(commandProvider);

    public Task<OwnedDataReader> ExecuteReaderAsync(string query, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<OwnedDataReader> ExecuteReaderAsync(string template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(CreateTemplate(template), parameters, cancellationToken);

    public Task<OwnedDataReader> ExecuteReaderAsync(InlineTemplateCommand template, IDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(template.Render(parameters), cancellationToken);

    public Task<OwnedDataReader> ExecuteReaderAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default)
        => _executor.ExecuteOwnedReaderAsync(commandProvider, cancellationToken);

    #endregion

    private T? NormalizeReturnType<T>(object obj)
    {
        var caster = ConnectionUrl.Dialect.Casters
                        .FirstOrDefault(x => x.CanCast(obj.GetType(), typeof(T)))
                        ?? throw new InvalidOperationException($"Cannot find any caster to transform from type '{obj.GetType().Name}' to the type '{typeof(T).Name}'");
        return (T?)caster.Cast(obj);
    }
}

