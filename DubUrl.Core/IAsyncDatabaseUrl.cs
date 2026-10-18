using DubUrl.Querying;

namespace DubUrl;

public interface IAsyncDatabaseUrl : IDatabaseUrl
{
    Task<object?> ReadScalarAsync(string query, CancellationToken cancellationToken = default);
    Task<object?> ReadScalarAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);
    Task<T?> ReadScalarAsync<T>(string query, CancellationToken cancellationToken = default);
    Task<T?> ReadScalarAsync<T>(ICommandProvider query, CancellationToken cancellationToken = default);
    Task<object> ReadScalarNonNullAsync(string query, CancellationToken cancellationToken = default);
    Task<object> ReadScalarNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);
    Task<T> ReadScalarNonNullAsync<T>(string query, CancellationToken cancellationToken = default);
    Task<T> ReadScalarNonNullAsync<T>(ICommandProvider query, CancellationToken cancellationToken = default);

    Task<object?> ReadSingleAsync(string query, CancellationToken cancellationToken = default);
    Task<object?> ReadSingleAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);
    Task<object> ReadSingleNonNullAsync(string query, CancellationToken cancellationToken = default);
    Task<object> ReadSingleNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);

    Task<object?> ReadFirstAsync(string query, CancellationToken cancellationToken = default);
    Task<object?> ReadFirstAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);
    Task<object> ReadFirstNonNullAsync(string query, CancellationToken cancellationToken = default);
    Task<object> ReadFirstNonNullAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);

    IAsyncEnumerable<object> ReadMultipleAsync(string query, CancellationToken cancellationToken = default);
    IAsyncEnumerable<object> ReadMultipleAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);

    Task<OwnedDataReader> ExecuteReaderAsync(string query, CancellationToken cancellationToken = default);
    Task<OwnedDataReader> ExecuteReaderAsync(ICommandProvider commandProvider, CancellationToken cancellationToken = default);
}
