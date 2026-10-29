using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

[Category("NativeCredentialStore")]
public class LinuxNamedCredentialStoreTests
{
    [Test]
    public async Task LockedCollectionPreservesPresenceAndRejectsSecretOperations()
    {
        if (!OperatingSystem.IsLinux() || Environment.GetEnvironmentVariable("DUBURL_TEST_ISOLATED_SECRET_SERVICE") != "1")
            Assert.Ignore("Runs only in the disposable credential-store-smoke container; never lock a user's desktop collection.");
        var name = "test-" + Guid.NewGuid().ToString("N");
        var console = new LockedConsole();
        await using var services = CliComposition.CreateDefault(console);
        var store = services.GetRequiredService<INamedCredentialStore>();
        await store.AddAsync(name, new("alice", "test-only"), false);
        LockPrivateCollection();
        // The container owns this entire default collection and destroys it on exit.
        // Do not request an unlock or try to alter any host desktop state.
        Assert.That(await store.ExistsAsync(name), Is.True);
        await Assert.ThrowsAsync<CredentialStoreException>(async () => await store.ReadAsync(name));
        Assert.That(await CliHost.InvokeAsync(["credential", "exists", name], services), Is.Zero);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", name, "--username", "bob", "--replace", "--password-stdin"], services), Is.EqualTo(10));
        Assert.That(await CliHost.InvokeAsync(["credential", "remove", name], services), Is.EqualTo(10));
        Assert.That(console.Errors.ToString(), Does.Not.Contain("test-only"));
        Assert.That(console.Output.ToString(), Is.Empty);
    }

    private static void LockPrivateCollection()
    {
        var service = secret_service_get_sync(0, IntPtr.Zero, out var error);
        Check(error);
        Assert.That(service, Is.Not.EqualTo(IntPtr.Zero));
        var collection = IntPtr.Zero;
        var list = IntPtr.Zero;
        var locked = IntPtr.Zero;
        try
        {
            collection = secret_collection_for_alias_sync(service, "default", 0, IntPtr.Zero, out error);
            Check(error);
            Assert.That(collection, Is.Not.EqualTo(IntPtr.Zero));
            list = g_list_append(IntPtr.Zero, collection);
            var count = secret_service_lock_sync(service, list, IntPtr.Zero, out locked, out error);
            Check(error);
            Assert.That(count, Is.GreaterThan(0));
        }
        finally
        {
            for (var node = locked; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size)) g_object_unref(Marshal.ReadIntPtr(node));
            if (locked != IntPtr.Zero) g_list_free(locked);
            if (list != IntPtr.Zero) g_list_free(list);
            if (collection != IntPtr.Zero) g_object_unref(collection);
            g_object_unref(service);
        }
    }

    private static void Check(IntPtr error)
    {
        if (error == IntPtr.Zero) return;
        g_error_free(error);
        Assert.Fail("Could not lock the isolated fixture's default collection.");
    }

    private sealed class LockedConsole : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader In => new StringReader("test-only");
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
    }

    [DllImport("libsecret-1.so.0")] private static extern IntPtr secret_service_get_sync(int flags, IntPtr cancellable, out IntPtr error);
    [DllImport("libsecret-1.so.0")] private static extern IntPtr secret_collection_for_alias_sync(IntPtr service, [MarshalAs(UnmanagedType.LPUTF8Str)] string alias, int flags, IntPtr cancellable, out IntPtr error);
    [DllImport("libsecret-1.so.0")] private static extern int secret_service_lock_sync(IntPtr service, IntPtr objects, IntPtr cancellable, out IntPtr locked, out IntPtr error);
    [DllImport("libglib-2.0.so.0")] private static extern IntPtr g_list_append(IntPtr list, IntPtr value);
    [DllImport("libglib-2.0.so.0")] private static extern void g_list_free(IntPtr list);
    [DllImport("libglib-2.0.so.0")] private static extern void g_error_free(IntPtr error);
    [DllImport("libgobject-2.0.so.0")] private static extern void g_object_unref(IntPtr value);
}
