using System.Data;
using DubUrl.Querying.Templating;

namespace DubUrl;

public partial interface IDatabaseUrl
{
    object? ReadScalar<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalar(template.Bind(parameters));

    T? ReadScalar<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalar<T>(template.Bind(parameters));

    object ReadScalarNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalarNonNull(template.Bind(parameters));

    T ReadScalarNonNull<T, TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadScalarNonNull<T>(template.Bind(parameters));

    object? ReadSingle<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadSingle(template.Bind(parameters));

    object ReadSingleNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadSingleNonNull(template.Bind(parameters));

    object? ReadFirst<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadFirst(template.Bind(parameters));

    object ReadFirstNonNull<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadFirstNonNull(template.Bind(parameters));

    IEnumerable<object> ReadMultiple<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ReadMultiple(template.Bind(parameters));

    IDataReader ExecuteReader<TParameters>(TypedCommandTemplate<TParameters> template, TParameters parameters)
        => ExecuteReader(template.Bind(parameters));
}
