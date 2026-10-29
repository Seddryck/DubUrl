using DubUrl.Querying;
using DubUrl.Querying.Templating;

namespace DubUrl;

public partial interface IAsyncDatabaseUrl
{
    Task<object?> ReadScalarAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync(template.Bind(parameters), cancellationToken);

    Task<T?> ReadScalarAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync<T>(template.Bind(parameters), cancellationToken);

    Task<object> ReadScalarNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync(template.Bind(parameters), cancellationToken);

    Task<T> ReadScalarNonNullAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync<T>(template.Bind(parameters), cancellationToken);

    Task<object?> ReadSingleAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadSingleAsync(template.Bind(parameters), cancellationToken);

    Task<object> ReadSingleNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadSingleNonNullAsync(template.Bind(parameters), cancellationToken);

    Task<object?> ReadFirstAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadFirstAsync(template.Bind(parameters), cancellationToken);

    Task<object> ReadFirstNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadFirstNonNullAsync(template.Bind(parameters), cancellationToken);

    IAsyncEnumerable<object> ReadMultipleAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadMultipleAsync(template.Bind(parameters), cancellationToken);

    Task<OwnedDataReader> ExecuteReaderAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(template.Bind(parameters), cancellationToken);
}
