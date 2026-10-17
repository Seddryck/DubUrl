using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
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

namespace DubUrl.MicroOrm;

public class DatabaseUrl : DubUrl.DatabaseUrl
{
    private IReflectionCache ReflectionCache { get; set; } = new ReflectionCache();

    public DatabaseUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IReflectionCache reflectionCache, IQueryLogger logger)
        : base(connectionUrl, commandProvisionerFactory, logger)
    { ReflectionCache = reflectionCache; }

    public DatabaseUrl WithoutCache()
    {
        ReflectionCache = new NoneReflectionCache();
        return this;
    }

    #region Scalar

   
    #endregion

    #region Single

    public T? ReadSingle<T>(string query) where T : new()
        => ReadSingle<T>(new InlineCommand(query, QueryLogger));

    public T? ReadSingle<T>(ICommandProvider commandProvider) where T : new()
        => ExecuteReader(commandProvider, reader =>
        {
            if (!reader.Read())
                return (T?)(object?)null;

            var entity = reader.ToEntity<T>(ReflectionCache);
            return !reader.Read() ? entity : throw new InvalidOperationException();
        });

    public Task<T?> ReadSingleAsync<T>(string query, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<T?> ReadSingleAsync<T>(ICommandProvider commandProvider, CancellationToken cancellationToken = default) where T : new()
        => ExecuteReaderAsync(commandProvider, async (reader, token) =>
        {
            if (!await reader.ReadAsync(token).ConfigureAwait(false))
                return (T?)(object?)null;

            var entity = reader.ToEntity<T>(ReflectionCache);
            return !await reader.ReadAsync(token).ConfigureAwait(false) ? entity : throw new InvalidOperationException();
        }, cancellationToken);

    public T? ReadSingleNonNull<T>(string query) where T : new()
        => ReadSingleNonNull<T>(new InlineCommand(query, QueryLogger));

    public T? ReadSingleNonNull<T>(ICommandProvider commandProvider) where T : new()
        => ReadSingle<T>(commandProvider) ?? throw new InvalidOperationException();

    public async Task<T> ReadSingleNonNullAsync<T>(string query, CancellationToken cancellationToken = default) where T : new()
        => await ReadSingleNonNullAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public async Task<T> ReadSingleNonNullAsync<T>(ICommandProvider commandProvider, CancellationToken cancellationToken = default) where T : new()
        => await ReadSingleAsync<T>(commandProvider, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException();

    #endregion

    #region First 

    public T? ReadFirst<T>(string query) where T : new()
        => ReadFirst<T>(new InlineCommand(query, QueryLogger));

    public T? ReadFirst<T>(ICommandProvider commandProvider) where T : new()
        => ExecuteReader(commandProvider, reader => reader.Read() ? reader.ToEntity<T>(ReflectionCache) : (T?)(object?)null);

    public Task<T?> ReadFirstAsync<T>(string query, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken);

    public Task<T?> ReadFirstAsync<T>(ICommandProvider commandProvider, CancellationToken cancellationToken = default) where T : new()
        => ExecuteReaderAsync(commandProvider, async (reader, token) =>
            await reader.ReadAsync(token).ConfigureAwait(false) ? reader.ToEntity<T>(ReflectionCache) : (T?)(object?)null, cancellationToken);

    public T ReadFirstNonNull<T>(string query) where T : new()
        => ReadFirstNonNull<T>(new InlineCommand(query, QueryLogger));

    public T ReadFirstNonNull<T>(ICommandProvider commandProvider) where T : new()
        => ReadFirst<T>(commandProvider) ?? throw new InvalidOperationException();

    public async Task<T> ReadFirstNonNullAsync<T>(string query, CancellationToken cancellationToken = default) where T : new()
        => await ReadFirstNonNullAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken).ConfigureAwait(false);

    public async Task<T> ReadFirstNonNullAsync<T>(ICommandProvider commandProvider, CancellationToken cancellationToken = default) where T : new()
        => await ReadFirstAsync<T>(commandProvider, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException();

    #endregion

    #region Multiple

    public IEnumerable<T> ReadMultiple<T>(string query) where T : new()
        => ReadMultiple<T>(new InlineCommand(query, QueryLogger));

    public IEnumerable<T> ReadMultiple<T>(ICommandProvider commandProvider) where T : new()
        => Stream(commandProvider, reader => reader.ToEntity<T>(ReflectionCache));

    public IAsyncEnumerable<T> ReadMultipleAsync<T>(string query, CancellationToken cancellationToken = default) where T : new()
        => ReadMultipleAsync<T>(new InlineCommand(query, QueryLogger), cancellationToken);

    public IAsyncEnumerable<T> ReadMultipleAsync<T>(ICommandProvider commandProvider, CancellationToken cancellationToken = default) where T : new()
        => StreamAsync(commandProvider, reader => reader.ToEntity<T>(ReflectionCache), cancellationToken);

    #endregion

}

