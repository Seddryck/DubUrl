using System.Runtime.InteropServices;
using DubUrl.Authentication;

namespace DubUrl.Cli;

internal sealed class WindowsNamedCredentialStore : INativeNamedCredentialStore
{
    private const int NotFound = 1168;
    private static string GetTargetName(string name) => "DubUrl/" + name;

    public bool Exists(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // WinCred has no metadata-only API. Enumerate the exact target and inspect only its name/type;
        // never marshal or decode CredentialBlob during a presence check.
        if (!CredEnumerate(GetTargetName(name), 0, out var count, out var entries))
        {
            if (Marshal.GetLastWin32Error() == NotFound) return false;
            throw new CredentialStoreException();
        }
        try
        {
            for (var index = 0; index < count; index++)
            {
                var entry = Marshal.PtrToStructure<Credential>(Marshal.ReadIntPtr(entries, checked(index * IntPtr.Size)));
                if (entry.Type == 1 && string.Equals(Marshal.PtrToStringUni(entry.Target), GetTargetName(name), StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        finally { CredFree(entries); }
    }

    public UsernamePasswordCredentials Read(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!CredRead(GetTargetName(name), 1, 0, out var pointer)) throw new CredentialStoreException();
        try
        {
            var entry = Marshal.PtrToStructure<Credential>(pointer);
            if (entry.Size % 2 != 0 || entry.Size > 2560) throw new CredentialStoreException();
            return new(Marshal.PtrToStringUni(entry.Username), entry.Size == 0 ? string.Empty : Marshal.PtrToStringUni(entry.Blob, (int)entry.Size / 2));
        }
        finally { CredFree(pointer); }
    }

    public void Write(string name, UsernamePasswordCredentials credentials, CancellationToken token)
    {
        var storedUsername = credentials.Username ?? throw new CredentialStoreException();
        var storedPassword = credentials.Password ?? throw new CredentialStoreException();
        var target = Marshal.StringToHGlobalUni(GetTargetName(name));
        var username = Marshal.StringToHGlobalUni(storedUsername);
        var secret = Marshal.StringToHGlobalUni(storedPassword);
        try
        {
            var entry = new Credential { Type = 1, Target = target, Username = username, Blob = secret,
                Size = checked((uint)storedPassword.Length * 2), Persist = 2 }; // Local-machine persistence, current user.
            token.ThrowIfCancellationRequested();
            if (!CredWrite(ref entry, 0)) throw new CredentialStoreException();
        }
        finally
        {
            // Passwords may contain NUL; ZeroFreeGlobalAllocUnicode would stop at the first one.
            for (var index = 0; index <= storedPassword.Length; index++) Marshal.WriteInt16(secret, index * 2, 0);
            Marshal.FreeHGlobal(secret);
            Marshal.FreeHGlobal(username);
            Marshal.FreeHGlobal(target);
        }
    }

    public void Remove(string name, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!CredDelete(GetTargetName(name), 1, 0) && Marshal.GetLastWin32Error() != NotFound) throw new CredentialStoreException();
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredEnumerate(string filter, uint flags, out int count, out IntPtr entries);
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
