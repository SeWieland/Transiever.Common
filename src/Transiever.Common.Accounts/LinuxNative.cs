using System.Runtime.InteropServices;

namespace Transiever.Common.Accounts;

internal static class LinuxNative
{
    internal const string GLib = "libglib-2.0.so.0";
    private const string GObject = "libgobject-2.0.so.0";
    private const string Gio = "libgio-2.0.so.0";
    private const string Secret = "libsecret-1.so.0";

    internal static bool Succeeded(nint error)
    {
        if (error == 0) return true;
        g_error_free(error);
        return false;
    }

    [DllImport(Secret)] internal static extern nint secret_service_get_sync(int flags, nint cancellable, out nint error);
    [DllImport(Secret)] internal static extern nint secret_service_get_session_algorithms(nint service);
    [DllImport(Secret)] internal static extern nint secret_service_search_sync(nint service, nint schema, nint attributes, int flags, nint cancellable, out nint error);
    [DllImport(Secret)] internal static extern nint secret_collection_for_alias_sync(nint service, [MarshalAs(UnmanagedType.LPUTF8Str)] string alias, int flags, nint cancellable, out nint error);
    [DllImport(Secret)] internal static extern int secret_collection_get_locked(nint collection);
    [DllImport(Secret)] internal static extern int secret_item_get_locked(nint item);
    [DllImport(Secret)] internal static extern nint secret_item_get_secret(nint item);
    [DllImport(Secret)] internal static extern int secret_item_set_secret_sync(nint item, nint value, nint cancellable, out nint error);
    [DllImport(Secret)] internal static extern nint secret_value_get(nint value, out nuint length);
    [DllImport(Secret)] internal static extern nint secret_value_new(byte[] secret, nint length, [MarshalAs(UnmanagedType.LPUTF8Str)] string contentType);
    [DllImport(Secret)] internal static extern void secret_value_unref(nint value);
    [DllImport(Secret)] internal static extern nint secret_service_encode_dbus_secret(nint service, nint value);
    [DllImport(Gio)] internal static extern nint g_dbus_proxy_call_sync(nint proxy, [MarshalAs(UnmanagedType.LPUTF8Str)] string method, nint parameters, int flags, int timeout, nint cancellable, out nint error);
    [DllImport(GObject)] internal static extern void g_object_unref(nint obj);
    [DllImport(GLib)] internal static extern nint g_hash_table_new(nint hash, nint equals);
    [DllImport(GLib)] internal static extern int g_hash_table_insert(nint table, nint key, nint value);
    [DllImport(GLib)] internal static extern void g_hash_table_unref(nint table);
    [DllImport(GLib)] internal static extern void g_list_free(nint list);
    [DllImport(GLib)] private static extern void g_error_free(nint error);
    [DllImport(GLib)] internal static extern nint g_variant_new_string([MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(GLib)] internal static extern nint g_variant_new_boolean(int value);
    [DllImport(GLib)] internal static extern nint g_variant_new_dict_entry(nint key, nint value);
    [DllImport(GLib)] internal static extern nint g_variant_new_variant(nint value);
    [DllImport(GLib)] internal static extern nint g_variant_new_array(nint elementType, nint[] children, nuint count);
    [DllImport(GLib)] internal static extern nint g_variant_new_tuple(nint[] children, nuint count);
    [DllImport(GLib)] internal static extern nint g_variant_ref_sink(nint value);
    [DllImport(GLib)] internal static extern void g_variant_unref(nint value);
    [DllImport(GLib)] internal static extern nint g_variant_get_type_string(nint value);
    [DllImport(GLib)] internal static extern nint g_variant_get_child_value(nint value, nuint index);
    [DllImport(GLib)] internal static extern nint g_variant_get_string(nint value, nint length);
}
