using System.Text;
using System.Text.Json;
using System.IO.Pipelines;
using NUnit.Framework;

namespace DubUrl.Worker.Testing;

public class WorkerHostTests
{
    [Test]
    public async Task RegisteredMethod_BindsParameterObject_AndReturnsResult()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<EchoParameters, EchoResult>(
            "test.echo",
            "Echoes the supplied value.",
            (parameters, _) => ValueTask.FromResult(new EchoResult(parameters.Value)));
        using var response = await InvokeAsync(
            registry,
            """{"jsonrpc":"2.0","id":7,"method":"test.echo","params":{"value":"hello"}}""");

        Assert.Multiple(() =>
        {
            Assert.That(response.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(7));
            Assert.That(
                response.RootElement.GetProperty("result").GetProperty("value").GetString(),
                Is.EqualTo("hello"));
        });
    }

    [Test]
    public async Task RegisteredMethod_WithInvalidParameters_ReturnsInvalidParams()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<EchoParameters, EchoResult>(
            "test.echo",
            "Echoes the supplied value.",
            (parameters, _) => ValueTask.FromResult(new EchoResult(parameters.Value)));
        using var response = await InvokeAsync(
            registry,
            """{"jsonrpc":"2.0","id":8,"method":"test.echo","params":{"value":42}}""");

        Assert.That(
            response.RootElement.GetProperty("error").GetProperty("code").GetInt32(),
            Is.EqualTo(-32602));
    }

    [Test]
    public async Task RegisteredMethod_WithUnexpectedFailure_ReturnsSanitizedInternalError()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<EchoParameters, EchoResult>(
            "test.fail",
            "Fails without leaking details.",
            (_, _) => throw new InvalidOperationException("secret failure details"));

        using var response = await InvokeAsync(
            registry,
            """{"jsonrpc":"2.0","id":9,"method":"test.fail","params":{"value":"hello"}}""");
        var error = response.RootElement.GetProperty("error");

        Assert.Multiple(() =>
        {
            Assert.That(error.GetProperty("code").GetInt32(), Is.EqualTo(-32603));
            Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("The worker method failed."));
            Assert.That(response.RootElement.ToString(), Does.Not.Contain("secret failure details"));
        });
    }

    [Test]
    public async Task RegisteredMethod_WithApplicationFailure_PreservesApplicationError()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<EchoParameters, EchoResult>(
            "test.reject",
            "Rejects input.",
            (_, _) => throw new WorkerMethodException(1001, "The value was rejected.", new { reason = "invalid" }));

        using var response = await InvokeAsync(
            registry,
            """{"jsonrpc":"2.0","id":10,"method":"test.reject","params":{"value":"hello"}}""");
        var error = response.RootElement.GetProperty("error");

        Assert.Multiple(() =>
        {
            Assert.That(error.GetProperty("code").GetInt32(), Is.EqualTo(1001));
            Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("The value was rejected."));
            Assert.That(error.GetProperty("data").GetProperty("reason").GetString(), Is.EqualTo("invalid"));
        });
    }

    [Test]
    public async Task RegisteredMethod_ReceivesRequestCancellation()
    {
        var registry = new WorkerMethodRegistry();
        registry.Register<EchoParameters, EchoResult>(
            "test.wait",
            "Waits until cancelled.",
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new EchoResult("unreachable");
            });

        using var response = await InvokeAsync(
            registry,
            """{"jsonrpc":"2.0","id":11,"method":"test.wait","params":{"value":"hello"}}""",
            """{"jsonrpc":"2.0","method":"$/cancelRequest","params":{"id":11}}""");

        Assert.That(
            response.RootElement.GetProperty("error").GetProperty("code").GetInt32(),
            Is.EqualTo(-32800));
    }

    private static async Task<JsonDocument> InvokeAsync(
        WorkerMethodRegistry registry,
        params string[] requests)
    {
        var input = new Pipe();
        var output = new Pipe();
        await using var inputStream = input.Reader.AsStream();
        await using var outputStream = output.Writer.AsStream();
        using var outputReader = new StreamReader(output.Reader.AsStream());
        var host = WorkerHost.RunAsync(inputStream, outputStream, registry, "test-version");

        await input.Writer.WriteAsync(Encoding.UTF8.GetBytes(string.Join('\n', requests) + "\n"));
        var response = await outputReader.ReadLineAsync();
        await input.Writer.CompleteAsync();
        await host;

        return JsonDocument.Parse(response!);
    }

    private sealed record EchoParameters(string Value);

    private sealed record EchoResult(string Value);
}
