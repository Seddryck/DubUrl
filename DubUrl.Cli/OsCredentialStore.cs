using System.Diagnostics;
using System.Runtime.InteropServices;
using DubUrl.Authentication;

namespace DubUrl.Cli;

/// <summary>Read-only OS adapters. No plaintext files, implicit fallback, writes or enumeration.</summary>
internal sealed class OsCredentialStore : IOsCredentialStore
{
    public async ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(reference) || reference.Any(char.IsControl))
            throw new ConnectionAuthenticationException("CredentialResolutionFailed");
        try
        {
            if (OperatingSystem.IsWindows()) return ReadWindows(reference);
            if (OperatingSystem.IsMacOS()) return new(password: await RunAsync("/usr/bin/security", ["find-generic-password", "-s", reference, "-w"], cancellationToken));
            if (OperatingSystem.IsLinux()) return new(password: await RunAsync("secret-tool", ["lookup", "service", "duburl", "entry", reference], cancellationToken));
            throw new ConnectionAuthenticationException("UnsupportedAuthentication");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ConnectionAuthenticationException) { throw; }
        catch { throw new ConnectionAuthenticationException("CredentialResolutionFailed"); }
    }

    private static async Task<string> RunAsync(string file, string[] arguments, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(30));
        var start = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new ConnectionAuthenticationException("CredentialResolutionFailed");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(budget.Token);
            var errors = process.StandardError.ReadToEndAsync(budget.Token);
            await process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            var value = await output.ConfigureAwait(false);
            _ = await errors.ConfigureAwait(false);
            if (process.ExitCode != 0 || value.Length == 0) throw new ConnectionAuthenticationException("CredentialResolutionFailed");
            // OS utilities append exactly one line terminator. Preserve all other password characters.
            return value.EndsWith("\r\n", StringComparison.Ordinal) ? value[..^2] : value.EndsWith('\n') ? value[..^1] : value;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
        }
    }

    private static UsernamePasswordCredentials ReadWindows(string reference)
    {
        if (!CredRead(reference, 1, 0, out var pointer)) throw new ConnectionAuthenticationException("CredentialResolutionFailed");
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.Size % 2 != 0) throw new ConnectionAuthenticationException("CredentialResolutionFailed");
            return new(Marshal.PtrToStringUni(credential.Username), Marshal.PtrToStringUni(credential.Blob, checked((int)credential.Size / 2)));
        }
        finally { CredFree(pointer); }
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
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
