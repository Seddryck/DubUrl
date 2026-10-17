using System.Text.Json;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace DubUrl.Worker;

internal sealed class ProtocolErrorMessageHandler(IJsonRpcMessageHandler inner) : IJsonRpcMessageHandler
{
    public bool CanRead => inner.CanRead;

    public bool CanWrite => inner.CanWrite;

    public IJsonRpcMessageFormatter Formatter => inner.Formatter;

    public async ValueTask<JsonRpcMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await inner.ReadAsync(cancellationToken);
        }
        catch (JsonException)
        {
            await WriteErrorAsync(JsonRpcErrorCode.ParseError, "Parse error.", cancellationToken);
            throw;
        }
        catch (UnrecognizedJsonRpcMessageException)
        {
            await WriteErrorAsync(JsonRpcErrorCode.InvalidRequest, "Invalid request.", cancellationToken);
            throw;
        }
    }

    public ValueTask WriteAsync(JsonRpcMessage jsonRpcMessage, CancellationToken cancellationToken)
        => inner.WriteAsync(jsonRpcMessage, cancellationToken);

    private async ValueTask WriteErrorAsync(
        JsonRpcErrorCode code,
        string message,
        CancellationToken cancellationToken)
    {
        var factory = (IJsonRpcMessageFactory)Formatter;
        var error = factory.CreateErrorMessage();
        error.RequestId = new RequestId((string?)null);
        error.Error = new JsonRpcError.ErrorDetail
        {
            Code = code,
            Message = message,
        };
        await inner.WriteAsync(error, cancellationToken);
    }
}
