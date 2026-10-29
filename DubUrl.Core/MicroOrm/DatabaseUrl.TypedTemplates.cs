using DubUrl.Querying.Templating;

namespace DubUrl.MicroOrm;

public partial class DatabaseUrl
{
    public T? ReadSingle<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters) where T : new()
        => ReadSingle<T>(template.Bind(parameters));

    public Task<T?> ReadSingleAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleAsync<T>(template.Bind(parameters), cancellationToken);

    public T? ReadSingleNonNull<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters) where T : new()
        => ReadSingleNonNull<T>(template.Bind(parameters));

    public Task<T> ReadSingleNonNullAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default) where T : new()
        => ReadSingleNonNullAsync<T>(template.Bind(parameters), cancellationToken);

    public T? ReadFirst<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters) where T : new()
        => ReadFirst<T>(template.Bind(parameters));

    public Task<T?> ReadFirstAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstAsync<T>(template.Bind(parameters), cancellationToken);

    public T ReadFirstNonNull<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters) where T : new()
        => ReadFirstNonNull<T>(template.Bind(parameters));

    public Task<T> ReadFirstNonNullAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default) where T : new()
        => ReadFirstNonNullAsync<T>(template.Bind(parameters), cancellationToken);

    public IEnumerable<T> ReadMultiple<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters) where T : new()
        => ReadMultiple<T>(template.Bind(parameters));

    public IAsyncEnumerable<T> ReadMultipleAsync<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters, CancellationToken cancellationToken = default) where T : new()
        => ReadMultipleAsync<T>(template.Bind(parameters), cancellationToken);
}
