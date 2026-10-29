using System.Runtime.InteropServices;
using System.Text.Json;
using DubUrl.Authentication;

namespace DubUrl.Cli;

/// <summary>Uses native libsecret, never secret-tool arguments or a file backend.</summary>
internal sealed class LinuxNamedCredentialStore : INativeNamedCredentialStore
{
    public bool Exists(string name, CancellationToken token)
    {
        using var session = new Session(name, token);
        using var items = session.Search(); // Neither UNLOCK nor LOAD_SECRETS flags.
        return items.Items.Count > 0;
    }

    public UsernamePasswordCredentials Read(string name, CancellationToken token)
    {
        using var session = new Session(name, token);
        using var items = session.Search();
        if (items.Items.Count != 1) throw new CredentialStoreException();
        var item = items.Items[0];
        session.RequireUnlocked(item);
        var loaded = secret_item_load_secret_sync(item, session.Cancellable, out var error);
        session.Check(error);
        if (!loaded) throw new CredentialStoreException();
        var value = secret_item_get_secret(item);
        if (value == IntPtr.Zero) throw new CredentialStoreException();
        try
        {
            var pointer = secret_value_get(value, out var size);
            if (size > 32768) throw new CredentialStoreException();
            var bytes = new byte[(int)size];
            try
            {
                Marshal.Copy(pointer, bytes, 0, bytes.Length);
                var payload = JsonSerializer.Deserialize<Payload>(bytes) ?? throw new CredentialStoreException();
                if (payload.Username is null || payload.Password is null) throw new CredentialStoreException();
                return new(payload.Username, payload.Password);
            }
            finally { Array.Clear(bytes); }
        }
        finally { secret_value_unref(value); }
    }

    public void Write(string name, UsernamePasswordCredentials credentials, CancellationToken token)
    {
        using var session = new Session(name, token);
        using var items = session.Search();
        if (items.Items.Count > 1) throw new CredentialStoreException();
        session.RequireUnlocked();
        foreach (var item in items.Items) session.RequireUnlocked(item);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Payload { Username = credentials.Username, Password = credentials.Password });
        try
        {
            var value = secret_value_new(bytes, bytes.Length, "application/json");
            if (value == IntPtr.Zero) throw new CredentialStoreException();
            try
            {
                token.ThrowIfCancellationRequested();
                var stored = secret_service_store_sync(session.Service, IntPtr.Zero, session.Attributes,
                    "/org/freedesktop/secrets/aliases/default", "DubUrl/" + name, value, session.Cancellable, out var error);
                session.Check(error);
                if (!stored) throw new CredentialStoreException();
            }
            finally { secret_value_unref(value); }
        }
        finally { Array.Clear(bytes); }
    }

    public void Remove(string name, CancellationToken token)
    {
        using var session = new Session(name, token);
        using var items = session.Search();
        if (items.Items.Count == 0) return;
        session.RequireUnlocked();
        foreach (var item in items.Items) session.RequireUnlocked(item);
        foreach (var item in items.Items)
        {
            token.ThrowIfCancellationRequested();
            var deleted = secret_item_delete_sync(item, session.Cancellable, out var error);
            session.Check(error);
            if (!deleted) throw new CredentialStoreException();
        }
    }

    private sealed class Payload
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    private sealed class Session : IDisposable
    {
        private readonly CancellationToken token;
        private readonly CancellationTokenSource budget;
        private readonly CancellationTokenRegistration registration;
        private readonly List<IntPtr> strings = [];
        private readonly IntPtr collection;
        public IntPtr Service { get; private set; }
        public IntPtr Attributes { get; private set; }
        public IntPtr Cancellable { get; }

        public Session(string name, CancellationToken token)
        {
            this.token = token;
            budget = CancellationTokenSource.CreateLinkedTokenSource(token);
            budget.CancelAfter(TimeSpan.FromSeconds(30));
            Cancellable = g_cancellable_new();
            registration = budget.Token.Register(() => g_cancellable_cancel(Cancellable));
            try
            {
                Service = secret_service_get_sync(0, Cancellable, out var error);
                Check(error);
                if (Service == IntPtr.Zero) throw new CredentialStoreException();
                collection = secret_collection_for_alias_sync(Service, "default", 0, Cancellable, out error);
                Check(error);
                if (collection == IntPtr.Zero) throw new CredentialStoreException();
                Attributes = g_hash_table_new(NativeLibrary.GetExport(GlibLibrary, "g_str_hash"), NativeLibrary.GetExport(GlibLibrary, "g_str_equal"));
                if (Attributes == IntPtr.Zero) throw new CredentialStoreException();
                AddAttribute("application", "duburl");
                AddAttribute("credential", name);
            }
            catch { Dispose(); throw; }
        }

        public ItemList Search()
        {
            token.ThrowIfCancellationRequested();
            // ALL=2, without UNLOCK=4 or LOAD_SECRETS=8: locked entries remain discoverable.
            var list = secret_collection_search_sync(collection, IntPtr.Zero, Attributes, 2, Cancellable, out var error);
            var items = new ItemList(list);
            try { Check(error); return items; }
            catch { items.Dispose(); throw; }
        }

        public void RequireUnlocked(IntPtr item = default)
        {
            if (secret_collection_get_locked(collection) || (item != IntPtr.Zero && secret_item_get_locked(item)))
                throw new CredentialStoreException(); // Unlock externally; never launch an implicit GUI prompt.
        }

        public void Check(IntPtr error)
        {
            if (error != IntPtr.Zero) g_error_free(error); // Backend text can contain secrets; discard it.
            token.ThrowIfCancellationRequested();
            if (error != IntPtr.Zero || budget.IsCancellationRequested) throw new CredentialStoreException();
        }

        [DllImport(NativeLibraryName)] private static extern IntPtr secret_service_get_sync(int flags, IntPtr cancellable, out IntPtr error);
        [DllImport(NativeLibraryName)] private static extern IntPtr secret_collection_for_alias_sync(IntPtr service, [MarshalAs(UnmanagedType.LPUTF8Str)] string alias, int flags, IntPtr cancellable, out IntPtr error);
        [DllImport(NativeLibraryName)] private static extern IntPtr secret_collection_search_sync(IntPtr collection, IntPtr schema, IntPtr attributes, int flags, IntPtr cancellable, out IntPtr error);
        [DllImport(NativeLibraryName)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool secret_collection_get_locked(IntPtr collection);
        [DllImport(NativeLibraryName)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool secret_item_get_locked(IntPtr item);
        [DllImport(Glib)] private static extern IntPtr g_hash_table_new(IntPtr hash, IntPtr equal);
        [DllImport(Glib)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool g_hash_table_insert(IntPtr table, IntPtr key, IntPtr value);
        [DllImport(Glib)] private static extern void g_hash_table_destroy(IntPtr table);
        [DllImport(Glib)] private static extern void g_error_free(IntPtr error);
        [DllImport(Gio)] private static extern IntPtr g_cancellable_new();
        [DllImport(Gio)] private static extern void g_cancellable_cancel(IntPtr cancellable);

        private void AddAttribute(string name, string value)
        {
            var key = Marshal.StringToCoTaskMemUTF8(name);
            strings.Add(key);
            var text = Marshal.StringToCoTaskMemUTF8(value);
            strings.Add(text);
            _ = g_hash_table_insert(Attributes, key, text);
        }

        public void Dispose()
        {
            registration.Dispose();
            budget.Dispose();
            if (Attributes != IntPtr.Zero) g_hash_table_destroy(Attributes);
            foreach (var text in strings) Marshal.FreeCoTaskMem(text);
            if (collection != IntPtr.Zero) g_object_unref(collection);
            if (Service != IntPtr.Zero) g_object_unref(Service);
            if (Cancellable != IntPtr.Zero) g_object_unref(Cancellable);
        }
    }

    private sealed class ItemList(IntPtr list) : IDisposable
    {
        public List<IntPtr> Items { get; } = GetItems(list);
        [DllImport(Glib)] private static extern void g_list_free(IntPtr list);
        private static List<IntPtr> GetItems(IntPtr head)
        {
            var items = new List<IntPtr>();
            for (var node = head; node != IntPtr.Zero; node = Marshal.ReadIntPtr(node, IntPtr.Size))
                items.Add(Marshal.ReadIntPtr(node));
            return items;
        }
        public void Dispose()
        {
            foreach (var item in Items) g_object_unref(item);
            if (list != IntPtr.Zero) g_list_free(list);
        }
    }

    private const string NativeLibraryName = "libsecret-1.so.0";
    private const string Glib = "libglib-2.0.so.0";
    private const string GObject = "libgobject-2.0.so.0";
    private const string Gio = "libgio-2.0.so.0";
    private static readonly IntPtr GlibLibrary = NativeLibrary.Load(Glib);
    [DllImport(NativeLibraryName)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool secret_item_load_secret_sync(IntPtr item, IntPtr cancellable, out IntPtr error);
    [DllImport(NativeLibraryName)] private static extern IntPtr secret_item_get_secret(IntPtr item);
    [DllImport(NativeLibraryName)] private static extern IntPtr secret_value_get(IntPtr value, out nuint length);
    [DllImport(NativeLibraryName)] private static extern IntPtr secret_value_new(byte[] secret, nint length, [MarshalAs(UnmanagedType.LPUTF8Str)] string contentType);
    [DllImport(NativeLibraryName)] private static extern void secret_value_unref(IntPtr value);
    [DllImport(NativeLibraryName)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool secret_service_store_sync(IntPtr service, IntPtr schema, IntPtr attributes, [MarshalAs(UnmanagedType.LPUTF8Str)] string collection, [MarshalAs(UnmanagedType.LPUTF8Str)] string label, IntPtr value, IntPtr cancellable, out IntPtr error);
    [DllImport(NativeLibraryName)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool secret_item_delete_sync(IntPtr item, IntPtr cancellable, out IntPtr error);
    [DllImport(GObject)] private static extern void g_object_unref(IntPtr instance);
}
