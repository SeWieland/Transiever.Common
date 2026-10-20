using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Transiever.Common.Accounts;

internal interface ILinuxCredentialSession : IDisposable
{
    LinuxCredentialMatches Search(string key, bool loadSecrets);
    NativeCredentialRead Read(nint item);
    int Update(nint item, string value);
    int Create(string key, string value);
    int Delete(nint item);
}

internal sealed class LinuxCredentialMatches(int status, IReadOnlyList<nint> items, Action<nint>? release = null) : IDisposable
{
    public int Status { get; } = status;
    public IReadOnlyList<nint> Items { get; } = items;
    public void Dispose()
    {
        if (release is not null)
            foreach (var item in Items) release(item);
    }
}

internal sealed class LinuxCredentialApi(Func<ILinuxCredentialSession?>? openSession = null) : INativeCredentialApi
{
    internal const int NotFound = 1;
    private const int Unavailable = 2;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly Func<ILinuxCredentialSession?> _openSession = openSession ?? Session.Open;

    public NativeCredentialRead Read(string key)
    {
        using var session = _openSession();
        if (session is null) return new(Unavailable);
        using var items = session.Search(key, loadSecrets: true);
        if (items.Status != 0) return new(items.Status);
        if (items.Items.Count != 1) return new(Unavailable);
        return session.Read(items.Items[0]);
    }

    public int Write(string key, string value)
    {
        using var session = _openSession();
        if (session is null) return Unavailable;
        using var items = session.Search(key, loadSecrets: false);
        if (items.Status == NotFound && items.Items.Count == 0) return session.Create(key, value);
        if (items.Status != 0 || items.Items.Count != 1) return Unavailable;
        return session.Update(items.Items[0], value);
    }

    public int Delete(string key)
    {
        using var session = _openSession();
        if (session is null) return Unavailable;
        using var items = session.Search(key, loadSecrets: false);
        if (items.Status != 0) return items.Status;
        if (items.Items.Count != 1) return Unavailable;
        return session.Delete(items.Items[0]);
    }

    private sealed class Session(nint service) : ILinuxCredentialSession
    {
        public nint Service { get; } = service;

        public static ILinuxCredentialSession? Open()
        {
            var service = LinuxNative.secret_service_get_sync(2, 0, out var error);
            var success = LinuxNative.Succeeded(error);
            if (service == 0) return null;
            var algorithms = Marshal.PtrToStringUTF8(LinuxNative.secret_service_get_session_algorithms(service));
            if (success && algorithms == "dh-ietf1024-sha256-aes128-cbc-pkcs7") return new Session(service);
            LinuxNative.g_object_unref(service);
            return null;
        }

        public LinuxCredentialMatches Search(string key, bool loadSecrets)
        {
            using var attributes = new Attributes(key);
            // Search locked items too, but never request unlocking or perform a service prompt.
            var list = LinuxNative.secret_service_search_sync(Service, 0, attributes.Handle, loadSecrets ? 10 : 2, 0, out var error);
            var success = LinuxNative.Succeeded(error);
            var items = new List<nint>();
            for (var node = list; node != 0; node = Marshal.ReadIntPtr(node, IntPtr.Size)) items.Add(Marshal.ReadIntPtr(node));
            LinuxNative.g_list_free(list);
            var status = !success || items.Any(item => LinuxNative.secret_item_get_locked(item) != 0) ? Unavailable
                : items.Count == 0 ? NotFound : 0;
            return new LinuxCredentialMatches(status, items, LinuxNative.g_object_unref);
        }

        public NativeCredentialRead Read(nint item)
        {
            var value = LinuxNative.secret_item_get_secret(item);
            if (value == 0) return new(Unavailable);
            try
            {
                var pointer = LinuxNative.secret_value_get(value, out var size);
                if (pointer == 0) return new(Unavailable);
                var bytes = new byte[checked((int)size)];
                try
                {
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    return new(0, Utf8.GetString(bytes));
                }
                catch (DecoderFallbackException) { return new(Unavailable); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            finally { LinuxNative.secret_value_unref(value); }
        }

        public int Create(string key, string value)
        {
            var collection = LinuxNative.secret_collection_for_alias_sync(Service, "default", 0, 0, out var error);
            if (!LinuxNative.Succeeded(error))
            {
                if (collection != 0) LinuxNative.g_object_unref(collection);
                return Unavailable;
            }
            if (collection == 0) return Unavailable;
            try
            {
                if (LinuxNative.secret_collection_get_locked(collection) != 0) return Unavailable;
                return CreateOrReplace(collection, key, value);
            }
            finally { LinuxNative.g_object_unref(collection); }
        }

        public int Update(nint item, string text)
        {
            var bytes = Utf8.GetBytes(text);
            nint value = 0;
            try
            {
                value = LinuxNative.secret_value_new(bytes, bytes.Length, "application/json");
                if (value == 0) return Unavailable;
                // SetSecret updates this item through its existing encrypted session without unlocking or prompting.
                var success = LinuxNative.secret_item_set_secret_sync(item, value, 0, out var error);
                var noError = LinuxNative.Succeeded(error);
                return success != 0 && noError ? 0 : Unavailable;
            }
            finally
            {
                if (value != 0) LinuxNative.secret_value_unref(value);
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        public int Delete(nint item) => CallWithoutPrompt(item, "Delete", 0, "(o)", 0) ? 0 : Unavailable;

        public int CreateOrReplace(nint collection, string key, string text)
        {
            var bytes = Utf8.GetBytes(text);
            nint secret = 0;
            nint parameters = 0;
            nint encoded = 0;
            try
            {
                secret = LinuxNative.secret_value_new(bytes, bytes.Length, "application/json");
                encoded = LinuxNative.secret_service_encode_dbus_secret(Service, secret);
                if (encoded == 0) return Unavailable;
                // libsecret returns a floating variant; retain our reference separately from the tuple's reference.
                LinuxNative.g_variant_ref_sink(encoded);
                var attributes = LinuxNative.g_variant_new_array(0,
                [
                    StringEntry("application", "Transiever.Accounts"),
                    StringEntry("account", key)
                ], 2);
                var properties = LinuxNative.g_variant_new_array(0,
                [
                    VariantEntry("org.freedesktop.Secret.Item.Label", LinuxNative.g_variant_new_string("Transiever account credential")),
                    VariantEntry("org.freedesktop.Secret.Item.Attributes", attributes)
                ], 2);
                parameters = LinuxNative.g_variant_new_tuple([properties, encoded, LinuxNative.g_variant_new_boolean(1)], 3);
                LinuxNative.g_variant_ref_sink(parameters);
                return CallWithoutPrompt(collection, "CreateItem", parameters, "(oo)", 1) ? 0 : Unavailable;
            }
            finally
            {
                if (parameters != 0) LinuxNative.g_variant_unref(parameters);
                if (encoded != 0) LinuxNative.g_variant_unref(encoded);
                if (secret != 0) LinuxNative.secret_value_unref(secret);
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        public static bool CallWithoutPrompt(nint proxy, string method, nint parameters, string replyType, nuint promptIndex)
        {
            // High-level libsecret create/delete helpers may display prompts; inspect the raw reply instead.
            var reply = LinuxNative.g_dbus_proxy_call_sync(proxy, method, parameters, 0, 30000, 0, out var error);
            var success = LinuxNative.Succeeded(error);
            if (reply == 0) return false;
            try
            {
                if (!success || Marshal.PtrToStringUTF8(LinuxNative.g_variant_get_type_string(reply)) != replyType) return false;
                var prompt = LinuxNative.g_variant_get_child_value(reply, promptIndex);
                try { return NativeSecretStore.CompletedWithoutPrompt(Marshal.PtrToStringUTF8(LinuxNative.g_variant_get_string(prompt, 0))); }
                finally { LinuxNative.g_variant_unref(prompt); }
            }
            finally { LinuxNative.g_variant_unref(reply); }
        }

        private static nint StringEntry(string key, string value) => LinuxNative.g_variant_new_dict_entry(
            LinuxNative.g_variant_new_string(key), LinuxNative.g_variant_new_string(value));

        private static nint VariantEntry(string key, nint value) => LinuxNative.g_variant_new_dict_entry(
            LinuxNative.g_variant_new_string(key), LinuxNative.g_variant_new_variant(value));

        public void Dispose() => LinuxNative.g_object_unref(Service);
    }

    private sealed class Attributes : IDisposable
    {
        private readonly nint _library = NativeLibrary.Load(LinuxNative.GLib);
        private readonly List<nint> _strings = [];
        public nint Handle { get; }

        public Attributes(string key)
        {
            Handle = LinuxNative.g_hash_table_new(NativeLibrary.GetExport(_library, "g_str_hash"), NativeLibrary.GetExport(_library, "g_str_equal"));
            Add("application", "Transiever.Accounts");
            Add("account", key);
        }

        private void Add(string key, string value)
        {
            var name = Marshal.StringToCoTaskMemUTF8(key);
            var text = Marshal.StringToCoTaskMemUTF8(value);
            _strings.Add(name);
            _strings.Add(text);
            LinuxNative.g_hash_table_insert(Handle, name, text);
        }

        public void Dispose()
        {
            LinuxNative.g_hash_table_unref(Handle);
            foreach (var text in _strings) Marshal.FreeCoTaskMem(text);
            NativeLibrary.Free(_library);
        }
    }
}
