using System.Diagnostics;
using System.Text.Json;
using NUnit.Framework;

namespace DubUrl.Worker.Testing;

public class WorkerProcessTests
{
    [Test]
    public async Task Version_ReturnsInformationalVersionOnStdout()
    {
        using var process = StartWorker("--version");

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(process.ExitCode, Is.Zero);
            Assert.That(output.Trim(), Is.Not.Empty);
            Assert.That(process.StandardError.ReadToEnd(), Is.Empty);
        });
    }

    [Test]
    public async Task Capabilities_PreservesRequestId_AndKeepsStdoutProtocolOnly()
    {
        using var process = StartWorker();
        await process.StandardInput.WriteLineAsync(
            """{"jsonrpc":"2.0","id":"request-42","method":"worker.capabilities"}""");

        var responseLine = await process.StandardOutput.ReadLineAsync();
        using var response = JsonDocument.Parse(responseLine!);

        Assert.Multiple(() =>
        {
            Assert.That(response.RootElement.GetProperty("jsonrpc").GetString(), Is.EqualTo("2.0"));
            Assert.That(response.RootElement.GetProperty("id").GetString(), Is.EqualTo("request-42"));
            Assert.That(
                response.RootElement.GetProperty("result").GetProperty("methods")
                    .EnumerateArray().Select(method => method.GetProperty("name").GetString()),
                Does.Contain("worker.capabilities"));
            Assert.That(
                response.RootElement.GetProperty("result").GetProperty("features")
                    .GetProperty("schema").GetProperty("documentVersions")[0].GetInt32(),
                Is.EqualTo(1));
        });

        await ShutdownAsync(process);
        Assert.That(await process.StandardOutput.ReadToEndAsync(), Is.Empty);
    }

    [Test]
    public async Task UnknownMethod_ReturnsMethodNotFound_AndAcceptsAnotherRequest()
    {
        using var process = StartWorker();
        await process.StandardInput.WriteLineAsync(
            """{"jsonrpc":"2.0","id":1,"method":"does.not.exist"}""");
        await process.StandardInput.WriteLineAsync(
            """{"jsonrpc":"2.0","id":2,"method":"worker.capabilities"}""");

        using var firstResponse = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync())!);
        using var secondResponse = JsonDocument.Parse((await process.StandardOutput.ReadLineAsync())!);
        var responses = new[] { firstResponse.RootElement, secondResponse.RootElement }
            .ToDictionary(response => response.GetProperty("id").GetInt32());

        Assert.Multiple(() =>
        {
            Assert.That(responses[1].GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32601));
            Assert.That(responses[2].TryGetProperty("result", out _), Is.True);
        });

        await ShutdownAsync(process);
    }

    [Test]
    public async Task MalformedJson_ReturnsParseError_AndTerminatesConnection()
    {
        using var process = StartWorker();
        await process.StandardInput.WriteLineAsync("not-json");

        var responseLine = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
        using var response = JsonDocument.Parse(responseLine!);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(response.RootElement.GetProperty("id").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(response.RootElement.GetProperty("error").GetProperty("code").GetInt32(), Is.EqualTo(-32700));
            Assert.That(process.ExitCode, Is.Zero);
        });
    }

    private static Process StartWorker(string? arguments = null)
    {
        var workerAssembly = typeof(WorkerHost).Assembly.Location;
        var startInfo = new ProcessStartInfo(
            "dotnet",
            $"\"{workerAssembly}\" {arguments}".TrimEnd())
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "0";
        startInfo.Environment["COR_ENABLE_PROFILING"] = "0";

        var process = Process.Start(startInfo);

        return process ?? throw new InvalidOperationException("The worker process could not be started.");
    }

    private static async Task ShutdownAsync(Process process)
    {
        await process.StandardInput.WriteLineAsync(
            """{"jsonrpc":"2.0","method":"worker.shutdown"}""");
        process.StandardInput.Close();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);
        Assert.That(process.ExitCode, Is.Zero, await process.StandardError.ReadToEndAsync());
    }
}
