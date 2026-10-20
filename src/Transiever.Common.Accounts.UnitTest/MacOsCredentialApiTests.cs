namespace Transiever.Common.Accounts.UnitTest;

public sealed class MacOsCredentialApiTests
{
    [Fact]
    public void Every_account_operation_uses_the_scoped_data_protection_keychain_without_authentication_ui()
    {
        var native = new FakeKeychain { UpdateStatus = MacOsCredentialApi.NotFound };
        var api = new MacOsCredentialApi(native);
        Assert.Equal("private-native-value", api.Read("scoped-account-key").Value);
        Assert.Equal(0, api.Write("scoped-account-key", "private-replacement"));
        Assert.Equal(0, api.Delete("scoped-account-key"));

        Assert.Equal(["Read", "Update", "Add", "Delete"], native.Calls.Select(call => call.Operation));
        foreach (var call in native.Calls)
        {
            Assert.Equal("kCFBooleanTrue", call.Query.Entries.GetValueOrDefault("kSecUseDataProtectionKeychain"));
            Assert.Equal("kSecUseAuthenticationUIFail", call.Query.Entries["kSecUseAuthenticationUI"]);
            Assert.Equal("kSecClassGenericPassword", call.Query.Entries["kSecClass"]);
            Assert.Equal("Transiever.Accounts", call.Query.Entries["kSecAttrService"]);
            Assert.Equal("scoped-account-key", call.Query.Entries["kSecAttrAccount"]);
        }
    }

    [Theory]
    [InlineData(-34018)]
    [InlineData(-25308)]
    public void Missing_entitlements_and_disallowed_interaction_fail_closed_without_legacy_retries(int nativeError)
    {
        var native = new FakeKeychain { ReadStatus = nativeError, UpdateStatus = nativeError, DeleteStatus = nativeError };
        var store = new NativeSecretStore(new MacOsCredentialApi(native), MacOsCredentialApi.NotFound);

        var read = store.Read("scoped-account-key");
        Assert.Equal(CredentialStatus.Unavailable, read.Status);
        Assert.Null(read.Secret);
        Assert.Equal(CredentialStatus.Unavailable, store.Write("scoped-account-key", "private-replacement"));
        Assert.Equal(CredentialStatus.Unavailable, store.Delete("scoped-account-key"));
        Assert.Equal(["Read", "Update", "Delete"], native.Calls.Select(call => call.Operation));
        Assert.All(native.Calls, call => Assert.Equal("kCFBooleanTrue", call.Query.Entries.GetValueOrDefault("kSecUseDataProtectionKeychain")));
    }

    private sealed class FakeKeychain : IMacOsKeychainApi
    {
        public int ReadStatus { get; init; }
        public int UpdateStatus { get; init; }
        public int DeleteStatus { get; init; }
        public List<(string Operation, FakeQuery Query)> Calls { get; } = [];
        public IMacOsKeychainQuery CreateQuery() => new FakeQuery();

        public NativeCredentialRead Read(IMacOsKeychainQuery query)
        {
            Calls.Add(("Read", (FakeQuery)query));
            return new(ReadStatus, "private-native-value");
        }

        public int Update(IMacOsKeychainQuery query, IMacOsKeychainQuery attributes)
        {
            Calls.Add(("Update", (FakeQuery)query));
            return UpdateStatus;
        }

        public int Add(IMacOsKeychainQuery query)
        {
            Calls.Add(("Add", (FakeQuery)query));
            return 0;
        }

        public int Delete(IMacOsKeychainQuery query)
        {
            Calls.Add(("Delete", (FakeQuery)query));
            return DeleteStatus;
        }
    }

    private sealed class FakeQuery : IMacOsKeychainQuery
    {
        public nint Handle => 0;
        public Dictionary<string, string> Entries { get; } = [];
        public void SetConstant(string key, string library, string value) => Entries[key] = value;
        public void SetString(string key, string value) => Entries[key] = value;
        public void SetData(string key, byte[] bytes) { }
        public void Dispose() { }
    }
}
