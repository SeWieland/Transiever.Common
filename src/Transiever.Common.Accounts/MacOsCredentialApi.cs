using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Transiever.Common.Accounts;

internal interface IMacOsKeychainQuery : IDisposable
{
    nint Handle { get; }
    void SetConstant(string key, string library, string value);
    void SetString(string key, string value);
    void SetData(string key, byte[] bytes);
}

internal interface IMacOsKeychainApi
{
    IMacOsKeychainQuery CreateQuery();
    NativeCredentialRead Read(IMacOsKeychainQuery query);
    int Update(IMacOsKeychainQuery query, IMacOsKeychainQuery attributes);
    int Add(IMacOsKeychainQuery query);
    int Delete(IMacOsKeychainQuery query);
}

internal sealed class MacOsCredentialApi(IMacOsKeychainApi? native = null) : INativeCredentialApi
{
    internal const int NotFound = -25300;
    private const string Security = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private readonly IMacOsKeychainApi _native = native ?? new NativeApi();

    public NativeCredentialRead Read(string key)
    {
        using var query = CreateAccountQuery(key);
        query.SetConstant("kSecReturnData", CoreFoundation, "kCFBooleanTrue");
        query.SetConstant("kSecMatchLimit", Security, "kSecMatchLimitOne");
        return _native.Read(query);
    }

    public int Write(string key, string value)
    {
        using var query = CreateAccountQuery(key);
        using var attributes = _native.CreateQuery();
        var bytes = Utf8.GetBytes(value);
        try
        {
            attributes.SetData("kSecValueData", bytes);
            var error = _native.Update(query, attributes);
            if (error != NotFound) return error;
            query.SetData("kSecValueData", bytes);
            return _native.Add(query);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public int Delete(string key)
    {
        using var query = CreateAccountQuery(key);
        return _native.Delete(query);
    }

    private IMacOsKeychainQuery CreateAccountQuery(string key)
    {
        var query = _native.CreateQuery();
        try
        {
            query.SetConstant("kSecClass", Security, "kSecClassGenericPassword");
            query.SetString("kSecAttrService", "Transiever.Accounts");
            query.SetString("kSecAttrAccount", key);
            query.SetConstant("kSecUseDataProtectionKeychain", CoreFoundation, "kCFBooleanTrue");
            // A locked keychain or an approval requirement must never open a UI in a headless caller.
            query.SetConstant("kSecUseAuthenticationUI", Security, "kSecUseAuthenticationUIFail");
            return query;
        }
        catch
        {
            query.Dispose();
            throw;
        }
    }

    private sealed class NativeApi : IMacOsKeychainApi
    {
        public IMacOsKeychainQuery CreateQuery() => new Query();
        public int Update(IMacOsKeychainQuery query, IMacOsKeychainQuery attributes) => SecItemUpdate(query.Handle, attributes.Handle);
        public int Add(IMacOsKeychainQuery query) => SecItemAdd(query.Handle, 0);
        public int Delete(IMacOsKeychainQuery query) => SecItemDelete(query.Handle);

        public NativeCredentialRead Read(IMacOsKeychainQuery query)
        {
            var error = SecItemCopyMatching(query.Handle, out var data);
            if (error != 0) return new(error);
            try
            {
                var bytes = new byte[checked((int)CFDataGetLength(data))];
                try
                {
                    Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, bytes.Length);
                    return new(0, Utf8.GetString(bytes));
                }
                catch (DecoderFallbackException) { return new(-50); }
                finally { CryptographicOperations.ZeroMemory(bytes); }
            }
            finally { CFRelease(data); }
        }
    }

    private sealed class Query : IMacOsKeychainQuery
    {
        private readonly List<nint> _owned = [];
        private readonly Dictionary<string, nint> _libraries = [];
        public nint Handle { get; } = CFDictionaryCreateMutable(0, 0, 0, 0);

        public void SetConstant(string key, string library, string value) => CFDictionarySetValue(Handle, Constant(Security, key), Constant(library, value));

        public void SetString(string key, string value)
        {
            var text = CFStringCreateWithCString(0, value, 0x08000100);
            _owned.Add(text);
            CFDictionarySetValue(Handle, Constant(Security, key), text);
        }

        public void SetData(string key, byte[] bytes)
        {
            var data = CFDataCreate(0, bytes, bytes.Length);
            _owned.Add(data);
            CFDictionarySetValue(Handle, Constant(Security, key), data);
        }

        private nint Constant(string library, string name)
        {
            if (!_libraries.TryGetValue(library, out var handle)) _libraries[library] = handle = NativeLibrary.Load(library);
            return Marshal.ReadIntPtr(NativeLibrary.GetExport(handle, name));
        }

        public void Dispose()
        {
            CFRelease(Handle);
            foreach (var value in _owned) CFRelease(value);
            foreach (var library in _libraries.Values) NativeLibrary.Free(library);
        }
    }

    [DllImport(Security)] private static extern int SecItemCopyMatching(nint query, out nint result);
    [DllImport(Security)] private static extern int SecItemAdd(nint attributes, nint result);
    [DllImport(Security)] private static extern int SecItemUpdate(nint query, nint attributes);
    [DllImport(Security)] private static extern int SecItemDelete(nint query);
    [DllImport(CoreFoundation)] private static extern nint CFDictionaryCreateMutable(nint allocator, nint capacity, nint keyCallbacks, nint valueCallbacks);
    [DllImport(CoreFoundation)] private static extern void CFDictionarySetValue(nint dictionary, nint key, nint value);
    [DllImport(CoreFoundation)] private static extern nint CFStringCreateWithCString(nint allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);
    [DllImport(CoreFoundation)] private static extern nint CFDataCreate(nint allocator, byte[] bytes, nint length);
    [DllImport(CoreFoundation)] private static extern nint CFDataGetLength(nint data);
    [DllImport(CoreFoundation)] private static extern nint CFDataGetBytePtr(nint data);
    [DllImport(CoreFoundation)] private static extern void CFRelease(nint value);
}
