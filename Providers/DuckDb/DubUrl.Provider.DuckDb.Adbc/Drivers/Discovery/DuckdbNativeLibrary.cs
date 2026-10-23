using System.Runtime.InteropServices;
using System.Text;

namespace DubUrl.Provider.DuckDb.Adbc.Drivers.Discovery;

internal static class DuckdbNativeLibrary
{
    public static string? Find()
    {
        var name = OperatingSystem.IsWindows() ? "duckdb.dll" : OperatingSystem.IsMacOS() ? "libduckdb.dylib" : "libduckdb.so";
        if (!NativeLibrary.TryLoad(name, typeof(DuckdbNativeLibrary).Assembly,
            DllImportSearchPath.SafeDirectories | DllImportSearchPath.AssemblyDirectory, out var handle))
            return null;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var buffer = new StringBuilder(32768);
                var length = GetModuleFileName(handle, buffer, buffer.Capacity);
                if (length == 0 || length >= buffer.Capacity)
                    throw new InvalidOperationException("Cannot resolve native driver path.");
                return buffer.ToString();
            }
            var symbol = NativeLibrary.GetExport(handle, DuckdbDriverDiscovery.DefaultEntryPoint);
            DlInfo info;
            int result;
            if (OperatingSystem.IsMacOS())
                result = MacDlAddr(symbol, out info);
            else
            {
                try { result = LibcDlAddr(symbol, out info); }
                catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
                { result = LinuxDlAddr(symbol, out info); }
            }
            var path = result == 0 ? null : Marshal.PtrToStringUTF8(info.Filename);
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("Cannot resolve native driver path.");
            return Path.GetFullPath(path);
        }
        finally { NativeLibrary.Free(handle); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo { public IntPtr Filename, BaseAddress, SymbolName, SymbolAddress; }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", CharSet = CharSet.Unicode)]
    private static extern int GetModuleFileName(IntPtr module, StringBuilder filename, int size);
    [DllImport("libc", EntryPoint = "dladdr")]
    private static extern int LibcDlAddr(IntPtr address, out DlInfo info);
    [DllImport("libdl.so.2", EntryPoint = "dladdr")]
    private static extern int LinuxDlAddr(IntPtr address, out DlInfo info);
    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dladdr")]
    private static extern int MacDlAddr(IntPtr address, out DlInfo info);
}
