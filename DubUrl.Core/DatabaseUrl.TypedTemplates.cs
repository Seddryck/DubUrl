using DubUrl.Querying;
using DubUrl.Querying.Templating;

namespace DubUrl;

public partial class DatabaseUrl
{
    public object? ReadScalar<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalar(template.Bind(parameters));

    public Task<object?> ReadScalarAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync(template.Bind(parameters), cancellationToken);

    public T? ReadScalar<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalar<T>(template.Bind(parameters));

    public Task<T?> ReadScalarAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarAsync<T>(template.Bind(parameters), cancellationToken);

    public object ReadScalarNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalarNonNull(template.Bind(parameters));

    public Task<object> ReadScalarNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync(template.Bind(parameters), cancellationToken);

    public T ReadScalarNonNull<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalarNonNull<T>(template.Bind(parameters));

    public Task<T> ReadScalarNonNullAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadScalarNonNullAsync<T>(template.Bind(parameters), cancellationToken);

    public object? ReadSingle<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadSingle(template.Bind(parameters));

    public Task<object?> ReadSingleAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadSingleAsync(template.Bind(parameters), cancellationToken);

    public object ReadSingleNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadSingleNonNull(template.Bind(parameters));

    public Task<object> ReadSingleNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadSingleNonNullAsync(template.Bind(parameters), cancellationToken);

    public object? ReadFirst<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadFirst(template.Bind(parameters));

    public Task<object?> ReadFirstAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadFirstAsync(template.Bind(parameters), cancellationToken);

    public object ReadFirstNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadFirstNonNull(template.Bind(parameters));

    public Task<object> ReadFirstNonNullAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadFirstNonNullAsync(template.Bind(parameters), cancellationToken);

    public IEnumerable<object> ReadMultiple<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadMultiple(template.Bind(parameters));

    public IAsyncEnumerable<object> ReadMultipleAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ReadMultipleAsync(template.Bind(parameters), cancellationToken);

    public OwnedDataReader ExecuteReader<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ExecuteReader(template.Bind(parameters));

    public Task<OwnedDataReader> ExecuteReaderAsync<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default)
        => ExecuteReaderAsync(template.Bind(parameters), cancellationToken);
}
