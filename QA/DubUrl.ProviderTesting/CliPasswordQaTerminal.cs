using System.Diagnostics;
using System.Text;
using DubUrl.Cli;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Runs the real CLI entry point and hidden-input adapter under an actual Linux pseudo-terminal.</summary>
internal static class CliPasswordQaTerminal
{
    internal static async Task VerifyAsync(PasswordQaDriver driver, string fixtureAssembly, bool escape, CliPasswordQaSchema? schema = null)
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("#1690: the existing QA has no Windows ConPTY/macOS native-terminal harness. Controlled prompt tests do not establish hidden-input behavior on those platforms.");
        if (!File.Exists("/usr/bin/script")) Assert.Ignore("#1690: the existing Linux QA image has no util-linux script pseudo-terminal utility; CI is unchanged.");
        var runtime = Path.ChangeExtension(fixtureAssembly, ".runtimeconfig.json");
        var deps = Path.ChangeExtension(fixtureAssembly, ".deps.json");
        if (!File.Exists(runtime) || !File.Exists(deps)) Assert.Ignore("#1690: the existing QA bundle has no executable runtime/dependency manifest for the real CLI terminal smoke test.");
        var reference = "DUBURL_PTY_QA_" + Guid.NewGuid().ToString("N");
        var schemaPath = Path.Combine(Path.GetTempPath(), "duburl-cli-qa-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            if (schema is not null) await File.WriteAllTextAsync(schemaPath, schema.Document);
            var arguments = new List<string> { "dotnet", "exec", "--runtimeconfig", runtime, "--depsfile", deps, typeof(CliHost).Assembly.Location };
            if (schema is null) arguments.AddRange(["connection", "check", "--engine"]);
            else arguments.AddRange(["schema", "deploy", schemaPath, "--input-format", "json"]);
            arguments.AddRange(["--connection-env", reference, "--username", driver.Uri.Username, "--password-prompt", "--output-format", "json"]);
            var start = new ProcessStartInfo("/usr/bin/script")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = AppContext.BaseDirectory };
            // Keep terminal echo enabled initially so this test verifies the CLI's hidden-input handling.
            foreach (var argument in new[] { "--quiet", "--return", "--echo", "always", "--command", string.Join(' ', arguments.Select(ShellQuote)), "/dev/null" })
                start.ArgumentList.Add(argument);
            start.Environment[reference] = driver.WithOptions(false);
            start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            await RunAsync(start, driver.Uri.Password, escape);
        }
        finally { if (File.Exists(schemaPath)) File.Delete(schemaPath); }
    }

    private static async Task RunAsync(ProcessStartInfo start, string password, bool escape)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var process = Process.Start(start) ?? throw new AssertionException("The CLI pseudo-terminal could not start.");
        var prompt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = CaptureAsync(process.StandardOutput, prompt, budget.Token);
        var error = CaptureAsync(process.StandardError, prompt, budget.Token);
        try
        {
            var exited = process.WaitForExitAsync(budget.Token);
            await Task.WhenAny(prompt.Task, exited).WaitAsync(budget.Token);
            Assert.That(prompt.Task.IsCompletedSuccessfully, Is.True, "The real CLI must reach its hidden password prompt; child output is suppressed.");
            await Task.Delay(100, budget.Token);
            // A backspace must remove the extra character before authentication. Escape is a real key, not a fake adapter cancellation.
            var keys = escape ? "\u001b" : password + "x\u007f\r";
            await process.StandardInput.WriteAsync(keys.AsMemory(), budget.Token);
            await process.StandardInput.FlushAsync(budget.Token);
            await exited;
            var text = await output + await error;
            new CliPasswordQaResult(process.ExitCode, text, "").AssertSafe(password);
            Assert.That(process.ExitCode, Is.EqualTo(escape ? 130 : 0), "The real CLI terminal run returned an unexpected exit code; sensitive output is suppressed.");
            if (!escape) Assert.That(text.Contains("\"success\": true", StringComparison.Ordinal), Is.True, "The real CLI must report successful database authentication/deployment.");
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
            try { await Task.WhenAll(output, error); }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task<string> CaptureAsync(StreamReader reader, TaskCompletionSource prompt, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[512];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            text.Append(buffer, 0, count);
            if (text.Length > 262144) throw new AssertionException("The CLI terminal produced excessive output; content is suppressed.");
            if (text.ToString().Contains("Password: ", StringComparison.Ordinal)) prompt.TrySetResult();
        }
        return text.ToString();
    }

    private static string ShellQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
