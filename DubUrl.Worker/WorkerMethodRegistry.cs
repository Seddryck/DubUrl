using System.Reflection;
using System.Text.Json;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace DubUrl.Worker;

public sealed class WorkerMethodRegistry
{
    private readonly List<WorkerMethodRegistration> _registrations = [];
    private readonly Dictionary<string, object> _features = new(StringComparer.Ordinal);

    public IReadOnlyList<WorkerCapability> Capabilities
        => _registrations
            .Select(registration => registration.Capability)
            .OrderBy(capability => capability.Name, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyDictionary<string, object> Features
        => new Dictionary<string, object>(_features, StringComparer.Ordinal);

    public void SetFeature<T>(string name, T value) where T : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_features.TryAdd(name, value))
            throw new InvalidOperationException($"A worker feature named '{name}' is already registered.");
    }

    public void Register<TParameters, TResult>(
        string name,
        string description,
        Func<TParameters, CancellationToken, ValueTask<TResult>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(handler);

        if (_registrations.Any(registration => registration.Capability.Name == name))
        {
            throw new InvalidOperationException($"A worker method named '{name}' is already registered.");
        }

        var target = new WorkerMethodTarget<TParameters, TResult>(handler);
        _registrations.Add(new WorkerMethodRegistration(
            new WorkerCapability(name, description),
            target,
            target.GetType().GetMethod(nameof(WorkerMethodTarget<TParameters, TResult>.InvokeAsync))!));
    }

    internal void AddMethodsTo(JsonRpc rpc)
    {
        foreach (var registration in _registrations)
        {
            rpc.AddLocalRpcMethod(
                registration.Method,
                registration.Target,
                new JsonRpcMethodAttribute(registration.Capability.Name)
                {
                    UseSingleObjectParameterDeserialization = true,
                });
        }
    }

    private sealed record WorkerMethodRegistration(
        WorkerCapability Capability,
        object Target,
        MethodInfo Method);

    private sealed class WorkerMethodTarget<TParameters, TResult>(
        Func<TParameters, CancellationToken, ValueTask<TResult>> handler)
    {
        private readonly JsonSerializerOptions _serializerOptions = WorkerJson.CreateSerializerOptions();

        public async ValueTask<TResult> InvokeAsync(
            JsonElement parameters,
            CancellationToken cancellationToken)
        {
            TParameters? typedParameters;
            try
            {
                typedParameters = parameters.Deserialize<TParameters>(_serializerOptions);
            }
            catch (JsonException)
            {
                throw InvalidParameters();
            }

            if (typedParameters is null)
            {
                throw InvalidParameters();
            }

            try
            {
                return await handler(typedParameters, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (LocalRpcException)
            {
                throw;
            }
            catch (WorkerMethodException exception)
            {
                throw new LocalRpcException(exception.Message)
                {
                    ErrorCode = exception.ErrorCode,
                    ErrorData = exception.ErrorDetails,
                };
            }
            catch (WorkerInvalidParametersException exception)
            {
                throw new LocalRpcException(exception.Message)
                {
                    ErrorCode = (int)JsonRpcErrorCode.InvalidParams,
                };
            }
            catch (Exception)
            {
                throw new LocalRpcException("The worker method failed.")
                {
                    ErrorCode = (int)JsonRpcErrorCode.InternalError,
                };
            }
        }

        private static LocalRpcException InvalidParameters()
            => new("Invalid method parameters.")
            {
                ErrorCode = (int)JsonRpcErrorCode.InvalidParams,
            };
    }
}
