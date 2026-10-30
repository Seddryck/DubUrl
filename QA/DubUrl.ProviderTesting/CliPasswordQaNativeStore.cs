using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DubUrl.Authentication;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Provisions only uniquely named fixture-owned entries; production adapters remain read-only.</summary>
internal sealed class CliPasswordQaNativeStore : IAsyncDisposable
{
    internal string Reference { get; } = "DubUrl-QA-" + Guid.NewGuid().ToString("N");
    private bool windows;
    private bool created;

    internal static CliPasswordQaNativeStore CreateWindows(UsernamePasswordCredentials credentials)
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("#1690: Windows Credential Manager cannot run on this QA operating system.");
        var entry = new CliPasswordQaNativeStore { windows = true };
        var target = Marshal.StringToCoTaskMemUni(entry.Reference);
        var username = Marshal.StringToCoTaskMemUni(credentials.Username ?? "qa-user");
        var password = Marshal.StringToCoTaskMemUni(credentials.Password ?? "");
        try
        {
            var value = new Credential
            {
                Type = 1, Target = target, Username = username, Blob = password,
                Size = checked((uint)((credentials.Password ?? "").Length * 2)), Persist = 1
            };
            if (!CredWrite(ref value, 0))
            {
                var code = Marshal.GetLastWin32Error();
                if (code is 5 or 50 or 1312) Assert.Ignore($"#1690: this runner has no usable current-user Credential Manager session (native code {code}).");
                Assert.Fail($"Fixture-owned Windows credential provisioning failed (native code {code}); no secret is reported.");
            }
            entry.created = true;
            return entry;
        }
        finally
        {
            Marshal.FreeCoTaskMem(target);
            Marshal.FreeCoTaskMem(username);
            Marshal.ZeroFreeCoTaskMemUnicode(password);
        }
    }

    internal static async Task<CliPasswordQaNativeStore> CreateLinuxAsync(UsernamePasswordCredentials credentials)
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("#1690: Linux Secret Service cannot run on this QA operating system.");
        if (!File.Exists("/usr/bin/secret-tool") || !File.Exists("/usr/bin/gdbus") || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
            Assert.Ignore("#1690: the existing headless QA runner has no provisioned secret-tool and usable Secret Service session bus. CI is unchanged; no keyring is installed or unlocked by this test.");
        var alias = await RunAsync("/usr/bin/gdbus", ["call", "--session", "--dest", "org.freedesktop.secrets", "--object-path", "/org/freedesktop/secrets", "--method", "org.freedesktop.Secret.Service.ReadAlias", "default"]);
        var match = Regex.Match(alias.Output, "'(?<path>/[^']+)'", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (alias.Exit != 0 || !match.Success) Assert.Ignore("#1690: this runner has no existing Secret Service default collection.");
        var locked = await RunAsync("/usr/bin/gdbus", ["call", "--session", "--dest", "org.freedesktop.secrets", "--object-path", match.Groups["path"].Value, "--method", "org.freedesktop.DBus.Properties.Get", "org.freedesktop.Secret.Collection", "Locked"]);
        if (locked.Exit != 0 || !locked.Output.Contains("false", StringComparison.Ordinal))
            Assert.Ignore("#1690: the existing Secret Service collection is unavailable or locked; the QA does not unlock or change the user's keyring.");
        var entry = new CliPasswordQaNativeStore();
        var stored = await RunAsync("/usr/bin/secret-tool", ["store", "--label=DubUrl fixture-owned QA entry", "service", "duburl", "entry", entry.Reference], (credentials.Password ?? "") + "\n");
        if (stored.Exit != 0) Assert.Fail("Fixture-owned Secret Service provisioning failed; sensitive helper output is suppressed.");
        entry.created = true;
        return entry;
    }

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        if (windows)
            Assert.That(CredDelete(Reference, 1, 0), Is.True, "The fixture-owned Windows credential must be removed.");
        else
        {
            var result = await RunAsync("/usr/bin/secret-tool", ["clear", "service", "duburl", "entry", Reference]);
            Assert.That(result.Exit, Is.Zero, "The fixture-owned Secret Service entry must be removed.");
        }
        created = false;
    }

    private static async Task<(int Exit, string Output)> RunAsync(string file, string[] arguments, string? input = null)
    {
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var start = new ProcessStartInfo(file)
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new AssertionException("The OS keyring helper could not start.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(budget.Token);
            var error = process.StandardError.ReadToEndAsync(budget.Token);
            if (input is not null) await process.StandardInput.WriteAsync(input.AsMemory(), budget.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(budget.Token);
            _ = await error;
            return (process.ExitCode, await output);
        }
        catch (OperationCanceledException) { return (-1, ""); }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public IntPtr Target;
        public IntPtr Comment;
        public long LastWritten;
        public uint Size;
        public IntPtr Blob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr Alias;
        public IntPtr Username;
    }
}
